namespace MyWebSocketServer;

// Simple session model
internal class ClientSession {
    internal ClientSession(string sessionId, int countA = 0, int countB = 0) {
        SessionId = sessionId;
        CountA = countA;
        CountB = countB;
    }

    public string SessionId { get; set; }
    public int CountA { get; set; }
    public int CountB { get; set; }
}
