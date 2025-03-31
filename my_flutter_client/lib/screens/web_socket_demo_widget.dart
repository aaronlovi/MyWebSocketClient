import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:web_socket_channel/web_socket_channel.dart';

class WebSocketDemoWidget extends StatefulWidget {
  const WebSocketDemoWidget({super.key});

  @override
  WebSocketDemoState createState() => WebSocketDemoState();
}

class WebSocketDemoState extends State<WebSocketDemoWidget> {
  WebSocketChannel? _channel;
  final String _webSocketUrl = 'wss://localhost:7167'; // WebSocket URL
  String _connectionStatus = 'Disconnected'; // Connection status text
  String _sessionId = '';
  int _countA = 0;
  int _countB = 0;
  Color _backgroundColor = Colors.white;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('WebSocket Demo')),
      backgroundColor: _backgroundColor,
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            ElevatedButton(onPressed: _connect, child: Text('Connect')),
            ElevatedButton(
              onPressed:
                  _connectionStatus == 'Connected'
                      ? () => _sendMessage('A')
                      : null,
              child: Text('Send A'),
            ),
            ElevatedButton(
              onPressed:
                  _connectionStatus == 'Connected'
                      ? () => _sendMessage('B')
                      : null,
              child: Text('Send B'),
            ),
            ElevatedButton(
              onPressed: _connectionStatus == 'Connected' ? _disconnect : null,
              child: Text('Disconnect'),
            ),
            Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start, // Align text to the left
              children: [
                Text(
                  'Connection Status: $_connectionStatus',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                Text(
                  'SessionId: $_sessionId',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                Text(
                  'CountA: $_countA',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                Text(
                  'CountB: $_countB',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  void _connect() {
    if (_channel != null && _connectionStatus == 'Connected') {
      _logWithTimestamp("Reconnecting to WebSocket...");
      _disconnect();
    }

    setState(() => _connectionStatus = 'Connecting...');
    try {
      _channel = WebSocketChannel.connect(Uri.parse(_webSocketUrl));

      _channel!.stream.listen(
        (message) {
          _logWithTimestamp("Received from server: $message");
          _processServerMessage(message);
        },
        onError: (error) {
          _logWithTimestamp("WebSocket error: $error");
          _resetSession('Error: $error');
        },
        onDone: () {
          if (!_connectionStatus.startsWith('Error')) {
            _disconnect();
          }
          _logWithTimestamp("WebSocket connection closed");
        },
      );
      setState(() => _connectionStatus = 'Connected');
    } catch (e) {
      _logWithTimestamp('Connection attempt failed: $e');
      _resetSession('Error: Unable to connect to the server');
    }
  }

  void _processServerMessage(String message) {
    try {
      final data = jsonDecode(message); // Parse the JSON message

      if (data.containsKey('R') &&
          data.containsKey('G') &&
          data.containsKey('B')) {
        final int r = data['R'];
        final int g = data['G'];
        final int b = data['B'];

        // Log the color values
        _logWithTimestamp("Received color message: R=$r, G=$g, B=$b");

        setState(() => _backgroundColor = Color.fromRGBO(r, g, b, 1.0));

        return;
      }

      setState(() {
        _sessionId = data['SessionId'] ?? ''; // Extract SessionId
        _countA = data['CountA'] ?? 0; // Extract CountA
        _countB = data['CountB'] ?? 0; // Extract CountB
      });
    } catch (e) {
      _logWithTimestamp("Error parsing server message: $e");
    }
  }

  void _sendMessage(String msg) {
    if (_channel == null || _connectionStatus != 'Connected') {
      _logWithTimestamp("Cannot send message. WebSocket is not connected.");
      return;
    }
    _channel!.sink.add(msg);
  }

  void _disconnect() {
    _channel?.sink.close();
    _channel = null;
    _resetSession('Disconnected');
  }

  void _resetSession(String connectionStatus) => setState(() {
    _connectionStatus = connectionStatus;
    _sessionId = '';
    _countA = _countB = 0;
  });

  void _logWithTimestamp(String message) {
    final timestamp = DateTime.now().toString();
    debugPrint("[$timestamp] $message");
  }
}
