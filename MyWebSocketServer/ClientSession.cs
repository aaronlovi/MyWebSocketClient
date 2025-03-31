using System.Net.WebSockets;
using System.Text.Json.Serialization;

namespace MyWebSocketServer;

// Simple session model
internal class ClientSession {
    internal ClientSession(string sessionId, WebSocket webSocket, int countA = 0, int countB = 0) {
        SessionId = sessionId;
        WebSocket = webSocket;
        CountA = countA;
        CountB = countB;
    }

    public string SessionId { get; set; }
    [JsonIgnore] public WebSocket WebSocket { get; set; }
    public int CountA { get; set; }
    public int CountB { get; set; }
}
