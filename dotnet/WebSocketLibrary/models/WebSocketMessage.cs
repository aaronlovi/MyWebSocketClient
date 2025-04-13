using System;
using System.Net.WebSockets;

namespace WebSocketLibrary.Models;

/// <summary>
/// Represents a WebSocket message with type and data.
/// </summary>
public class WebSocketMessage
{
    /// <summary>
    /// Creates a new WebSocketMessage with the specified type and data.
    /// </summary>
    /// <param name="messageType">The type of WebSocket message</param>
    /// <param name="data">The message data as bytes</param>
    /// <param name="endOfMessage">Flag indicating if this is the final part of the message</param>
    public WebSocketMessage(WebSocketMessageType messageType, byte[] data, bool endOfMessage = true)
    {
        MessageType = messageType;
        Data = data ?? throw new ArgumentNullException(nameof(data));
        EndOfMessage = endOfMessage;
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
    /// Creates a new text WebSocketMessage from the specified string.
    /// </summary>
    /// <param name="text">The text message content</param>
    /// <returns>A WebSocketMessage with the text content</returns>
    public static WebSocketMessage CreateTextMessage(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        
        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
        return new WebSocketMessage(WebSocketMessageType.Text, data);
    }
    
    /// <summary>
    /// Creates a new binary WebSocketMessage from the specified byte array.
    /// </summary>
    /// <param name="data">The binary message data</param>
    /// <returns>A WebSocketMessage with the binary data</returns>
    public static WebSocketMessage CreateBinaryMessage(byte[] data)
    {
        return new WebSocketMessage(WebSocketMessageType.Binary, data);
    }
    
    /// <summary>
    /// Converts a text WebSocketMessage to a string.
    /// </summary>
    /// <returns>The text content of the message, or null if this is not a text message</returns>
    public string? GetTextContent()
    {
        if (MessageType != WebSocketMessageType.Text)
        {
            return null;
        }
        
        return System.Text.Encoding.UTF8.GetString(Data);
    }
}