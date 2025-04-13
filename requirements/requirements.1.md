# Requirements

| Requirement id | Description | Status |
|----------------|-------------|--------|
| WS.1 | Accept WebSocket upgrade requests from clients. | |
| WS.2 | Send text or binary messages to connected WebSocket clients. | |
| WS.3 | Receive and process text or binary messages from WebSocket clients. | |
| WS.4 | Broadcast messages to multiple connected WebSocket clients. | |
| WS.5 | Handle WebSocket connection closures gracefully. | |
| WS.6 | Support WebSocket ping/pong frames for connection health checks. | |
| WS.7 | Handle errors during WebSocket communication. | |
| WS.8 | Authenticate and authorize WebSocket connections. | |
| WS.9 | Manage active WebSocket connections, including tracking and disconnecting idle clients. | |
| LIB.1 | Create the WebSocket library as a new class library project that can be imported into an ASP.NET Core 8.0 server application. The project should be called `WebSocketLibrary`. | Complete |
| LIB.2 | Configure the library using an `IOptions` class to allow users to customize settings such as connection timeouts, maximum message size, and authentication options. | |
| LIB.3 | Organize the library into the following folders: | |
|       | - `services`: For concrete implementations of services and singletons. | |
|       | - `models`: For implementations of data structures used in the library. | |
|       | - `contracts`: For interface classes corresponding to the services in the `services` folder. | |
|       | - `utilities`: For utility classes | |
| TEST.1 | Create a unit test project for each library project. Each unit test project should have the same name as the original library project, but with the suffix ".Tests". Unit tests should be in xUnit form. | |
| TEST.2 | Create a unit test file for each class under the `service` directory of each library project. Each unit test file should fully exercise the original class in the library project. | |
| DOC.1 | In every class file, in every project, create class and function documentation. For library code with scope `public`, the documentation point of view should be to inform both maintainer and library user. For library code with scope `private` or `internal`, the documentation point of view should be to inform the maintainer. | |
| DOC.2 | Write a Readme.md file. It should go in the root of the C# solution folder. The Readme.md file should describe the different projects, how to use them, and any prerequisites for running/compiling the projects. | |
| DOC.3 | Write a LICENSE file in the root of the C# solution folder. License should be restrictive, with all rights reserved. Any users should request permissions from Aaron Lovi (aaronlovi@gmail.com). | |

## Notes

Only modify files in the `dotnet` folder.
