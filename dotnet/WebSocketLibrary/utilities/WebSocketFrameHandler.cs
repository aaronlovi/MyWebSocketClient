using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketLibrary.Models;

namespace WebSocketLibrary.Utilities
{
    /// <summary>
    /// Handles WebSocket frame management, including fragmentation and reassembly of messages.
    /// </summary>
    public class WebSocketFrameHandler
    {
        private readonly ILogger _logger;
        private readonly WebSocketOptions _options;
        private readonly ConcurrentDictionary<string, MessageAssembler> _messageAssemblers = 
            new ConcurrentDictionary<string, MessageAssembler>();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketFrameHandler"/> class.
        /// </summary>
        /// <param name="options">WebSocket configuration options</param>
        /// <param name="logger">The logger</param>
        public WebSocketFrameHandler(WebSocketOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends a message, automatically fragmenting it if needed.
        /// </summary>
        /// <param name="webSocket">The WebSocket to send the message through</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SendMessageAsync(
            WebSocket webSocket,
            WebSocketMessage message,
            CancellationToken cancellationToken)
        {
            if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            // Fragment the message if it's larger than the maximum frame size
            var fragments = message.Fragment(_options.MaxFrameSize);
            
            foreach (var fragment in fragments)
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(fragment.Data),
                    fragment.MessageType,
                    fragment.EndOfMessage,
                    cancellationToken);
                
                _logger.LogDebug(
                    "Sent {MessageType} frame: {Size} bytes, EndOfMessage: {EndOfMessage}, FrameIndex: {FrameIndex}",
                    fragment.MessageType,
                    fragment.Size,
                    fragment.EndOfMessage,
                    fragment.FrameIndex);
            }
        }

        /// <summary>
        /// Processes a received message fragment and returns the complete message if all fragments 
        /// have been received.
        /// </summary>
        /// <param name="sessionId">The client session ID</param>
        /// <param name="fragment">The message fragment</param>
        /// <returns>The complete message if all fragments have been received, null otherwise</returns>
        public WebSocketMessage? ProcessFragment(string sessionId, WebSocketMessage fragment)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (fragment == null) throw new ArgumentNullException(nameof(fragment));
            
            // If not fragmented, return as is
            if (!fragment.IsFragment && fragment.FrameIndex == 0)
            {
                return fragment;
            }
            
            // Get or create a message assembler for this session
            var assembler = _messageAssemblers.GetOrAdd(sessionId, _ => new MessageAssembler());
            
            // Add the fragment to the assembler
            return assembler.AddFragment(fragment);
        }
        
        /// <summary>
        /// Clears any incomplete messages for a session (e.g., when a client disconnects).
        /// </summary>
        /// <param name="sessionId">The client session ID</param>
        public void ClearSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            
            _messageAssemblers.TryRemove(sessionId, out _);
        }
        
        /// <summary>
        /// Helper class to assemble fragmented messages.
        /// </summary>
        private class MessageAssembler
        {
            private readonly List<WebSocketMessage> _fragments = new List<WebSocketMessage>();
            private WebSocketMessageType _messageType = WebSocketMessageType.Binary;
            
            /// <summary>
            /// Adds a message fragment and returns the complete message if all fragments have been received.
            /// </summary>
            /// <param name="fragment">The message fragment to add</param>
            /// <returns>The complete message if all fragments have been received, null otherwise</returns>
            public WebSocketMessage? AddFragment(WebSocketMessage fragment)
            {
                // Store the message type from the first fragment
                if (_fragments.Count == 0)
                {
                    _messageType = fragment.MessageType;
                }
                else if (fragment.MessageType != _messageType)
                {
                    // According to the WebSocket protocol, all fragments of the same message
                    // must use the same opcode (Text or Binary)
                    throw new InvalidDataException(
                        $"Message type mismatch. Expected: {_messageType}, Received: {fragment.MessageType}");
                }
                
                // Add the fragment
                _fragments.Add(fragment);
                
                // If this is the end of the message, assemble it
                if (fragment.EndOfMessage)
                {
                    return AssembleMessage();
                }
                
                return null;
            }
            
            /// <summary>
            /// Assembles the complete message from all fragments.
            /// </summary>
            /// <returns>The complete message</returns>
            private WebSocketMessage AssembleMessage()
            {
                // Calculate the total size of the message
                int totalSize = 0;
                foreach (var fragment in _fragments)
                {
                    totalSize += fragment.Size;
                }
                
                // Create a buffer for the complete message
                byte[] completeData = new byte[totalSize];
                int offset = 0;
                
                // Copy all fragments into the buffer
                foreach (var fragment in _fragments)
                {
                    Buffer.BlockCopy(fragment.Data, 0, completeData, offset, fragment.Size);
                    offset += fragment.Size;
                }
                
                // Clear the fragments
                _fragments.Clear();
                
                // Create a new message with the assembled data
                return new WebSocketMessage(_messageType, completeData, true, 0);
            }
        }
    }
}