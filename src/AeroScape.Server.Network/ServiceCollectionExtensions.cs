using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Handlers;
using AeroScape.Server.Network.Pipeline;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace AeroScape.Server.Network;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAeroScapeNetwork(this IServiceCollection services)
    {
        // Protocol
        services.AddSingleton<ProtocolService>();
        
        // Session management
        services.AddSingleton<PlayerSessionManager>();
        
        // TCP server
        services.AddHostedService<TcpServerService>();

        // Pipeline (transient — one per connection)
        services.AddTransient<ConnectionPipeline>();
        services.AddTransient<PacketDispatcher>();

        // Message handlers (scoped — resolved per packet dispatch)
        services.AddScoped<IMessageHandler<WalkMessage>, WalkHandler>();
        services.AddScoped<IMessageHandler<CommandMessage>, CommandHandler>();
        services.AddScoped<IMessageHandler<KeepAliveMessage>, KeepAliveHandler>();
        services.AddScoped<IMessageHandler<IdleLogoutMessage>, IdleLogoutHandler>();
        services.AddScoped<IMessageHandler<RegionLoadedMessage>, RegionLoadedHandler>();
        services.AddScoped<IMessageHandler<CloseInterfaceMessage>, CloseInterfaceHandler>();
        services.AddScoped<IMessageHandler<AppearanceUpdateMessage>, AppearanceHandler>();
        services.AddScoped<IMessageHandler<ButtonClickMessage>, ButtonHandler>();
        services.AddScoped<IMessageHandler<EquipItemMessage>, EquipItemHandler>();
        services.AddScoped<IMessageHandler<DropItemMessage>, DropItemHandler>();

        return services;
    }
}
