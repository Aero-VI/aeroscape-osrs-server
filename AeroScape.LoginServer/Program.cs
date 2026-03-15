using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

class Program
{
    private const int Port = 43594;

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}] AeroScape Login Server starting on port {Port}...");

        // Initialise RSA keys (load from disk or generate new)
        RsaKeys.Initialize();

        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
        listener.Start();

        Console.WriteLine($"[{DateTime.UtcNow:u}] Server listening on 0.0.0.0:{Port}");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine($"[{DateTime.UtcNow:u}] Shutdown signal received. Stopping...");
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Disable Nagle algorithm for low-latency login packets
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                Console.WriteLine($"[{DateTime.UtcNow:u}] Client connected: {remoteEndpoint}");

                // Handle each client in its own task
                _ = Task.Run(async () =>
                {
                    var handler = new LoginHandler(client, remoteEndpoint, cts.Token);
                    try
                    {
                        await handler.HandleAsync();
                    }
                    finally
                    {
                        client.Dispose();
                        Console.WriteLine($"[{DateTime.UtcNow:u}] Client disconnected: {remoteEndpoint}");
                    }
                }, cts.Token);
            }
        }
        finally
        {
            listener.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:u}] Server stopped.");
        }
    }
}
