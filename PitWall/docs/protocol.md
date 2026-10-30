# PitWall — WebSocket Protocol

## Wire Format

All messages are JSON text frames over WebSocket, UTF-8 encoded.

Every message follows this envelope:
```json
{
  "type": "<message-type>",
  "seq": 12345,
  "t": 1698600000000,
  "payload": { ... }
}
```

- `type`: one of the message types below
- `seq`: monotonically increasing sequence number per sender
- `t`: host UTC timestamp in milliseconds
- `payload`: type-specific data

## Message Types

### Host → Client

| Type         | Frequency | Description                              |
|-------------|-----------|------------------------------------------|
| `live`      | 10 Hz     | Player telemetry, tyres, energy          |
| `field`     | 2 Hz      | Full field state, session, strategy      |
| `event`     | Immediate | Game events (SSTA, FTLP, etc.)           |
| `call`      | On action | Engineer call to driver                  |
| `callStatus`| On action | Call delivery/ack status update          |
| `roomStatus`| On change | Room settings and viewer count           |
| `viewerList`| On change | Connected viewers                        |
| `pong`      | On ping   | Response to ping                         |
| `error`     | On error  | Error notification                       |

### Client → Host / Relay

| Type      | Description                                    |
|----------|------------------------------------------------|
| `hello`  | Initial handshake with role and name           |
| `call`   | Engineer sends a radio call                    |
| `callAck`| Driver acknowledges a received call            |
| `ping`   | Latency measurement                            |

### Relay-Specific

| Type          | Direction      | Description                        |
|--------------|----------------|------------------------------------|
| `createRoom` | Host → Relay   | Request room creation              |
| `roomCreated`| Relay → Host   | Room code and join URL             |
| `joinRequest`| Relay → Host   | Viewer wants to join               |
| `joinApprove`| Host → Relay   | Approve a viewer                   |
| `joinDeny`   | Host → Relay   | Deny a viewer                      |
| `kickViewer` | Host → Relay   | Remove a viewer                    |
| `closeRoom`  | Host → Relay   | Close the room                     |
| `roomClosed` | Relay → Client | Room has been closed               |

## Size Limits

- Maximum message size: 64 KB
- Maximum call text: 120 characters (control characters stripped)
- Calls are rendered as text, never HTML

## Rate Limits

- Host snapshots: ≤ 20 messages/second
- Calls: ≤ 5 per 10 seconds per viewer
- Join attempts: ≤ 10/minute and 30/hour per IP
