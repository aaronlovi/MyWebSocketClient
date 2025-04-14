# Requirements

| Requirement id | Description | Status |
|----------------|-------------|--------|
| DOC.1 | Add XML documentation. Enable XML documentation in the project to provide better IntelliSense support.  Add the &lt;GenerateDocumentationFile&gt;true&lt;/GenerateDocumentationFile&gt; tag to .csproj files | Complete |
| XP.1 | Add cross-platform support. Target .net8, and .netstandard 2.0 for compatibility with .NET Framework. Note, if .Net standard 2.0 is not installed, then install it first. | Complete |
| IMP.1 | Enhance package references. Add System.IO.Pipelines for more efficient data processing, and use it in the project where appropriate. Use it to: Minimize memory allocations and buffer copying; Handle parsing protocols without excess memory allocations; Efficiently manage byte streams; Handle message fragments more efficiently; Reduce garbage collection pressure; Improve the throughput for large messages | Complete |
| IMP.2 | Source Link Support. Add SourceLink support for better debugging experience into .csproj files as appropriate. | Complete |
| IMP.3 | Implement message framing and fragmentation support. Add to unit tests in the WebSocketLibrary.Tests project | Complete |
| IMP.4 | Implement a heartbeat mechanism. Add to unit tests in the WebSocketLibrary.Tests project. | Replaced by sub-steps |
| IMP.4.1 | Create a HeartbeatService class in the WebSocketLibrary project with configuration options (ping interval, timeout threshold). | Complete |
| IMP.4.2 | Implement periodic ping mechanism in HeartbeatService that sends WebSocket ping frames at configurable intervals. | Complete |
| IMP.4.3 | Implement tracking of pong responses from clients in WebSocketClientSession class. | Complete |
| IMP.4.4 | Add timeout detection logic to identify unresponsive clients that fail to respond with pong frames. | Complete |
| IMP.4.5 | Implement client disconnection mechanism for clients that exceed the timeout threshold. | Complete |
| IMP.4.6 | Add unit tests for HeartbeatService in the WebSocketLibrary.Tests project. | Complete |
| IMP.4.7 | Improve timing in `StartAsync_SendsPingAtConfiguredInterval` test by replacing the fixed delay with a deterministic approach, such as using a mockable timer or event to trigger the pings. | Complete |
| IMP.4.8 | Enhance test coverage for `HeartbeatService` by adding tests for edge cases, such as when the WebSocket is not in an open state or when the cancellation token is triggered immediately. | Complete |
| IMP.4.9 | Refactor `HeartbeatService` to make the ping interval and timeout threshold configurable via dependency injection for better testability. | Complete |
| IMP.4.10 | Integrate HeartbeatService with the existing WebSocketHandler and middleware. | Pending |
| IMP.4.11 | Add unit tests for the integration of HeartbeatService with the existing WebSocketHandler and middleware. | Pending |
| IMP.5 | Add compression support (per RFC 7692). Add to unit tests in the WebSocketLibrary.Tests project. Add to unit tests in the WebSocketLibrary.Tests project | Pending |
| IMP.6 | Add message serialization helpers. Add to unit tests in the WebSocketLibrary.Tests project. Add JSON serialization/deserialization helpers to the `WebSocketMessage` class as appropriate. | Pending |
| IMP.7 | Add WebSocket subprotocol negotiation support. Allow the server to declare supported subprotocols and negotiate with clients. | Pending |
| IMP.7.1 | Add a property to WebSocketOptions for supported subprotocols (string array). | Pending |
| IMP.7.2 | Implement subprotocol selection during the WebSocket handshake process - select the first protocol from the client's list that matches the server's supported protocols. | Pending |
| IMP.7.3 | Store the negotiated subprotocol in WebSocketClientSession for use during message processing. | Pending |
| IMP.7.4 | Add helper methods that can process messages according to common subprotocols (MQTT, WAMP, STOMP). | Pending |
| IMP.7.5 | Add to unit tests in the WebSocketLibrary.Tests project to verify subprotocol negotiation. | Pending |
| SEC.1 | Add a rate limiting option per client, with a default of "no limit". Add to unit tests in the WebSocketLibrary.Tests project | Pending |
| SEC.2 | Request origin validation, with a list of allowed origins as server options. Add to unit tests in the WebSocketLibrary.Tests project.  Allow the facility of allowing all origins. | Pending |
| LIB.1 | Add package metadata for NuGet | Pending |
| LIB.2 | Consider Object Pooling for Buffers. Implement object pooling for frequently used buffers to reduce GC pressure. | Pending |
| LIB.3 | Implement binary serialization options. Add support for Protocol buffers for more efficient binary serialization. Add to unit tests in the WebSocketLibrary.Tests project | Pending |
| LIB.4 | Exception handling strategy: Clearly define how WebSocket exceptions are propagated or wrapped | Pending |
| LIB.5 | Cancellation support: Ensure all async operations respect cancellation tokens | Pending |

## Notes

- Only modify files in the `dotnet` folder.
- After each step, commit and push the work to git remote.
  - Add a one or two line description of the commit.
  - Use `git add .` rather than commit individual files.
- For unit tests, please use xUnit.
