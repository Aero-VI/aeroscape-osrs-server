# AeroScape Server

Server-side infrastructure for the AeroScape platform.

## Projects

### AeroScape.LoginServer

A raw TCP login server listening on port **43594**.

- Built with .NET 10
- Uses `TcpListener` for direct socket communication (no HTTP/web API)
- Handles client connections/disconnections with basic logging
- Designed for custom binary protocol implementation

## Requirements

- .NET 10 SDK

## Running

```bash
cd AeroScape.LoginServer
dotnet run
```

The server will start and listen on `0.0.0.0:43594`. Press `Ctrl+C` to stop.
