using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketLibrary.Models;

namespace WebSocketLibrary.Utilities
{
    /// <summary>
    /// Handles efficient WebSocket message processing using System.IO.Pipelines.
    /// Minimizes memory allocations and improves throughput for large messages.
    /// </summary>
    public class MessagePipeline
    {
        private readonly ILogger _logger;
        private readonly WebSocketOptions _options;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePipeline"/> class.
        /// </summary>
        /// <param name="options">The WebSocket configuration options</param>
        /// <param name="logger">The logger for message pipeline</param>
        public MessagePipeline(WebSocketOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Processes incoming WebSocket messages using efficient System.IO.Pipelines.
        /// </summary>
        /// <param name="webSocket">The WebSocket to process messages from</param>
        /// <param name="messageHandler">Action to handle processed messages</param>
        /// <param name="closeHandler">Action to handle WebSocket close requests</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ProcessWebSocketMessagesAsync(
            WebSocket webSocket, 
            Action<WebSocketMessage> messageHandler,
            Action<WebSocketCloseStatus?> closeHandler,
            CancellationToken cancellationToken)
        {
            if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));
            if (messageHandler == null) throw new ArgumentNullException(nameof(messageHandler));
            if (closeHandler == null) throw new ArgumentNullException(nameof(closeHandler));
            
            // Create a pipe with custom options for optimal performance
            var pipeOptions = new PipeOptions(
                minimumSegmentSize: _options.MaxMessageSize / 2,
                pauseWriterThreshold: _options.MaxMessageSize * 4,
                resumeWriterThreshold: _options.MaxMessageSize * 2,
                useSynchronizationContext: false);
                
            var pipe = new Pipe(pipeOptions);
            
            // Start the pipeline tasks
            Task fillPipeTask = FillPipeAsync(webSocket, pipe.Writer, cancellationToken);
            Task readPipeTask = ReadPipeAsync(pipe.Reader, webSocket, messageHandler, closeHandler, cancellationToken);
            
            // Wait for both tasks to complete
            await Task.WhenAll(fillPipeTask, readPipeTask);
        }
        
        private async Task FillPipeAsync(WebSocket webSocket, PipeWriter writer, CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[_options.MaxMessageSize];
                
                while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    // Receive data directly into byte array for .NET Standard 2.0 compatibility
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Complete the pipe writer and exit
                        await writer.CompleteAsync();
                        return;
                    }
                    
                    // Write the received data to the pipe
#if NET8_0
                    writer.Write(buffer.AsSpan(0, result.Count));
#else
                    // For .NET Standard 2.0, we need to write directly to the pipe buffer
                    Memory<byte> memory = writer.GetMemory(_options.MaxMessageSize);
                    // Copy our buffer to the pipe's memory
                    for (int i = 0; i < result.Count; i++)
                    {
                        memory.Span[i] = buffer[i];
                    }
                    writer.Advance(result.Count);
#endif
                    
                    // Make the data available to the reader
                    FlushResult flushResult = await writer.FlushAsync(cancellationToken);
                    
                    if (flushResult.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is WebSocketException)
            {
                // Handle expected exceptions
                _logger.LogDebug(ex, "WebSocket receive operation stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving WebSocket data");
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }
        
        private async Task ReadPipeAsync(
            PipeReader reader, 
            WebSocket webSocket, 
            Action<WebSocketMessage> messageHandler,
            Action<WebSocketCloseStatus?> closeHandler,
            CancellationToken cancellationToken)
        {
            try
            {
                WebSocketMessageType currentMessageType = WebSocketMessageType.Text;
                bool endOfMessage = false;
                
                using var messageStream = new MemoryStream();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    if (result.IsCanceled)
                    {
                        break;
                    }
                    
                    if (buffer.IsEmpty && result.IsCompleted)
                    {
                        // If WebSocket was closed, signal the close handler
                        if (webSocket.State == WebSocketState.CloseReceived ||
                            webSocket.State == WebSocketState.Closed)
                        {
                            closeHandler(webSocket.CloseStatus);
                        }
                        break;
                    }
                    
                    ProcessBuffer(buffer, messageStream, ref currentMessageType, ref endOfMessage);
                    
                    // Mark the consumed data
                    reader.AdvanceTo(buffer.End, buffer.End);
                    
                    if (endOfMessage)
                    {
                        // Create and process the complete message
                        byte[] messageData = messageStream.ToArray();
                        var message = new WebSocketMessage(currentMessageType, messageData);
                        
                        // Handle the message
                        messageHandler(message);
                        
                        // Reset for the next message
                        messageStream.SetLength(0);
                        endOfMessage = false;
                    }
                    
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket data");
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }
        
        private void ProcessBuffer(
            ReadOnlySequence<byte> buffer,
            MemoryStream messageStream,
            ref WebSocketMessageType messageType,
            ref bool endOfMessage)
        {
            // Process the buffer sequence
            if (buffer.IsSingleSegment)
            {
                // Fast path for single segments
                ProcessSegment(buffer.First, messageStream);
            }
            else
            {
                // Process each segment in the buffer
                foreach (var segment in buffer)
                {
                    ProcessSegment(segment, messageStream);
                }
            }
            
            // For simplicity in this implementation, always set endOfMessage to true
            endOfMessage = true;
        }
        
        private void ProcessSegment(ReadOnlyMemory<byte> segment, MemoryStream messageStream)
        {
            // Create a temporary buffer to hold this segment
            byte[] tempBuffer = new byte[segment.Length];
                
            // Copy the data to our temporary buffer in a .NET Standard 2.0 compatible way
            for (int i = 0; i < segment.Length; i++)
            {
                tempBuffer[i] = segment.Span[i];
            }
                
            // Write to the memory stream
            messageStream.Write(tempBuffer, 0, tempBuffer.Length);
        }
    }
}