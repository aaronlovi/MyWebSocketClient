using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketLibrary.Models
{
    /// <summary>
    /// Represents a WebSocket message with type and data.
    /// </summary>
    public class WebSocketMessage {
        /// <summary>
        /// Creates a new WebSocketMessage with the specified type and data.
        /// </summary>
        /// <param name="messageType">The type of WebSocket message</param>
        /// <param name="data">The message data as bytes</param>
        /// <param name="endOfMessage">Flag indicating if this is the final part of the message</param>
        /// <param name="frameIndex">The index of this frame in a multi-frame message (0-based)</param>
        public WebSocketMessage(
            WebSocketMessageType messageType, 
            byte[] data, 
            bool endOfMessage = true,
            int frameIndex = 0) {
            
            MessageType = messageType;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            EndOfMessage = endOfMessage;
            FrameIndex = frameIndex;
            IsFragment = !endOfMessage || frameIndex > 0;
        }

        /// <summary>
        /// The type of WebSocket message (Text, Binary, or Close).
        /// </summary>
        public WebSocketMessageType MessageType { get; }

        /// <summary>
        /// The message data as a byte array.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Flag indicating if this is the final part of a message.
        /// </summary>
        public bool EndOfMessage { get; }
        
        /// <summary>
        /// The position of this frame in a fragmented message sequence (0-based).
        /// </summary>
        public int FrameIndex { get; }
        
        /// <summary>
        /// Indicates whether this message is a fragment of a larger message.
        /// </summary>
        public bool IsFragment { get; }
        
        /// <summary>
        /// Gets the size of the message data in bytes.
        /// </summary>
        public int Size => Data?.Length ?? 0;

        /// <summary>
        /// Creates a new text WebSocketMessage from the specified string.
        /// </summary>
        /// <param name="text">The text message content</param>
        /// <param name="endOfMessage">Flag indicating if this is the final part of the message</param>
        /// <param name="frameIndex">The index of this frame in a multi-frame message (0-based)</param>
        /// <returns>A WebSocketMessage with the text content</returns>
        public static WebSocketMessage CreateTextMessage(
            string text, 
            bool endOfMessage = true,
            int frameIndex = 0) {
            
            if (text == null) throw new ArgumentNullException(nameof(text));

            byte[] data = Encoding.UTF8.GetBytes(text);
            return new WebSocketMessage(WebSocketMessageType.Text, data, endOfMessage, frameIndex);
        }

        /// <summary>
        /// Creates a new binary WebSocketMessage from the specified byte array.
        /// </summary>
        /// <param name="data">The binary message data</param>
        /// <param name="endOfMessage">Flag indicating if this is the final part of the message</param>
        /// <param name="frameIndex">The index of this frame in a multi-frame message (0-based)</param>
        /// <returns>A WebSocketMessage with the binary data</returns>
        public static WebSocketMessage CreateBinaryMessage(
            byte[] data, 
            bool endOfMessage = true,
            int frameIndex = 0) {
            
            return new WebSocketMessage(WebSocketMessageType.Binary, data, endOfMessage, frameIndex);
        }
        
        /// <summary>
        /// Fragments the message into multiple smaller messages based on the specified maximum frame size.
        /// </summary>
        /// <param name="maxFrameSize">The maximum size of each frame in bytes</param>
        /// <returns>A list of fragmented messages</returns>
        public IReadOnlyList<WebSocketMessage> Fragment(int maxFrameSize)
        {
            if (maxFrameSize <= 0) 
                throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "Max frame size must be positive");
            
            // If message is smaller than max frame size, return as is
            if (Data.Length <= maxFrameSize)
            {
                return new List<WebSocketMessage> { this };
            }
            
            var fragments = new List<WebSocketMessage>();
            int totalFragments = (int)Math.Ceiling((double)Data.Length / maxFrameSize);
            
            for (int i = 0; i < totalFragments; i++)
            {
                int offset = i * maxFrameSize;
                int length = Math.Min(maxFrameSize, Data.Length - offset);
                
                byte[] fragmentData = new byte[length];
                Buffer.BlockCopy(Data, offset, fragmentData, 0, length);
                
                bool isLastFragment = i == totalFragments - 1;
                
                fragments.Add(new WebSocketMessage(
                    MessageType,
                    fragmentData,
                    isLastFragment,
                    i));
            }
            
            return fragments;
        }

        /// <summary>
        /// Converts a text WebSocketMessage to a string.
        /// </summary>
        /// <returns>The text content of the message, or null if this is not a text message</returns>
        public string? GetTextContent() {
            return MessageType == WebSocketMessageType.Text
                ? Encoding.UTF8.GetString(Data) 
                : null;
        }
    }
}