using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

class Program
{
    private static readonly int[] Ports = { 43594, 443 };

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}] AeroScape Login Server starting on ports {string.Join(", ", Ports)}...");

        // Initialise RSA keys (load from disk or generate new)
        RsaKeys.Initialize();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine($"[{DateTime.UtcNow:u}] Shutdown signal received. Stopping...");
            cts.Cancel();
        };

        var listeners = new TcpListener[Ports.Length];
        var tasks = new Task[Ports.Length];

        for (int i = 0; i < Ports.Length; i++)
        {
            var port = Ports[i];
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            listener.Start();
            listeners[i] = listener;
            Console.WriteLine($"[{DateTime.UtcNow:u}] Server listening on 0.0.0.0:{port}");

            tasks[i] = AcceptLoop(listener, port, cts.Token);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var listener in listeners)
                listener.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:u}] Server stopped.");
        }
    }

    private static async Task AcceptLoop(TcpListener listener, int port, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Disable Nagle algorithm for low-latency login packets
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                Console.WriteLine($"[{DateTime.UtcNow:u}] Client connected on port {port}: {remoteEndpoint}");

                // Handle each client in its own task
                _ = Task.Run(async () =>
                {
                    var handler = new LoginHandler(client, remoteEndpoint, ct);
                    try
                    {
                        await handler.HandleAsync();
                    }
                    finally
                    {
                        client.Dispose();
                        Console.WriteLine($"[{DateTime.UtcNow:u}] Client disconnected: {remoteEndpoint}");
                    }
                }, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
}
