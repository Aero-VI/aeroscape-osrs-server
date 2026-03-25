using System.Net;
using System.Net.Sockets;
using AeroScape.Server.Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Tcp;

/// <summary>
/// High-performance TCP listener using async socket accept.
/// Runs as a BackgroundService, delegates each connection to the connection pipeline.
/// Uses System.IO.Pipelines internally for efficient zero-copy packet framing.
/// </summary>
public sealed class TcpServerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TcpServerService> _logger;
    private Socket? _listener;

    public TcpServerService(IServiceProvider serviceProvider, ILogger<TcpServerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(new IPEndPoint(IPAddress.Any, ServerConstants.Port));
        _listener.Listen(128);

        _logger.LogInformation("TCP server listening on port {Port} (Pipelines-backed)", ServerConstants.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener.AcceptAsync(stoppingToken);
                clientSocket.NoDelay = true;
                
                // Fire and forget — each connection handled independently
                _ = HandleConnectionAsync(clientSocket, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting connection");
            }
        }
    }

    private async Task HandleConnectionAsync(Socket socket, CancellationToken ct)
    {
        var endpoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.LogDebug("New connection from {Endpoint}", endpoint);

        try
        {
            var handler = _serviceProvider.GetRequiredService<Pipeline.ConnectionPipeline>();
            await handler.ProcessAsync(socket, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection from {Endpoint}", endpoint);
        }
        finally
        {
            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            socket.Dispose();
        }
    }

    public override void Dispose()
    {
        _listener?.Dispose();
        base.Dispose();
    }
}
