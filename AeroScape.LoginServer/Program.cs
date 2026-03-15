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

        var listener = new TcpListener(IPAddress.Any, Port);
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

                var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                Console.WriteLine($"[{DateTime.UtcNow:u}] Client connected: {remoteEndpoint}");

                _ = Task.Run(() => HandleClientAsync(client, remoteEndpoint, cts.Token), cts.Token);
            }
        }
        finally
        {
            listener.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:u}] Server stopped.");
        }
    }

    private static async Task HandleClientAsync(TcpClient client, string remoteEndpoint, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, ct);
                    }
                    catch (Exception)
                    {
                        break;
                    }

                    if (bytesRead == 0)
                        break;

                    // TODO: Process incoming packet data
                }
            }
        }
        finally
        {
            Console.WriteLine($"[{DateTime.UtcNow:u}] Client disconnected: {remoteEndpoint}");
        }
    }
}
