using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Decoders;
using AeroScape.Server.Network.Handlers;
using AeroScape.Server.Network.Login;
using AeroScape.Server.Network.Pipeline;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Js5;
using AeroScape.Server.Network.Tcp;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAeroScapeNetwork(this IServiceCollection services)
    {
        // Protocol
        services.AddSingleton<ProtocolService>();
        
        // JS5 cache service
        services.AddSingleton<Js5CacheService>();
        
        // Session management
        services.AddSingleton<PlayerSessionManager>();
        
        // Update service (game tick processor)
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IGameTickProcessor>(sp => sp.GetRequiredService<UpdateService>());
        
        // Login handler
        services.AddSingleton<LoginHandler>();
        
        // TCP server
        services.AddHostedService<TcpServerService>();

        // Pipeline (transient — one per connection)
        services.AddTransient<ConnectionPipeline>();

        // --- Packet Decoders (singleton — stateless byte→message transforms) ---
        services.AddSingleton<IPacketDecoder, WalkDecoder>();
        services.AddSingleton<IPacketDecoder, PublicChatDecoder>();
        services.AddSingleton<IPacketDecoder, CommandDecoder>();
        services.AddSingleton<IPacketDecoder, ButtonClickDecoder>();
        services.AddSingleton<IPacketDecoder, DialogueContinueDecoder>();
        services.AddSingleton<IPacketDecoder, CloseInterfaceDecoder>();
        services.AddSingleton<IPacketDecoder, AppearanceUpdateDecoder>();
        services.AddSingleton<IPacketDecoder, EquipItemDecoder>();
        services.AddSingleton<IPacketDecoder, UnequipItemDecoder>();
        services.AddSingleton<IPacketDecoder, DropItemDecoder>();
        services.AddSingleton<IPacketDecoder, MoveItemDecoder>();
        services.AddSingleton<IPacketDecoder, NpcInteractDecoder>();
        services.AddSingleton<IPacketDecoder, ObjectInteractDecoder>();
        services.AddSingleton<IPacketDecoder, PlayerInteractDecoder>();
        services.AddSingleton<IPacketDecoder, GroundItemInteractDecoder>();
        services.AddSingleton<IPacketDecoder, AddFriendDecoder>();
        services.AddSingleton<IPacketDecoder, RemoveFriendDecoder>();
        services.AddSingleton<IPacketDecoder, AddIgnoreDecoder>();
        services.AddSingleton<IPacketDecoder, RemoveIgnoreDecoder>();
        services.AddSingleton<IPacketDecoder, PrivateMessageDecoder>();
        services.AddSingleton<IPacketDecoder, KeepAliveDecoder>();
        services.AddSingleton<IPacketDecoder, IdleLogoutDecoder>();
        services.AddSingleton<IPacketDecoder, RegionLoadedDecoder>();
        services.AddSingleton<IPacketDecoder, FocusChangedDecoder>();

        // PacketRouter — built once at startup from all registered decoders
        services.AddSingleton<PacketRouter>(sp =>
            PacketRouter.Build(sp, sp.GetRequiredService<ILogger<PacketRouter>>()));

        // --- Message handlers (scoped — resolved per packet dispatch) ---
        services.AddScoped<IMessageHandler<WalkMessage>, WalkHandler>();
        services.AddScoped<IMessageHandler<CommandMessage>, CommandHandler>();
        services.AddScoped<IMessageHandler<PublicChatMessage>, ChatHandler>();
        services.AddScoped<IMessageHandler<KeepAliveMessage>, KeepAliveHandler>();
        services.AddScoped<IMessageHandler<IdleLogoutMessage>, IdleLogoutHandler>();
        services.AddScoped<IMessageHandler<RegionLoadedMessage>, RegionLoadedHandler>();
        services.AddScoped<IMessageHandler<CloseInterfaceMessage>, CloseInterfaceHandler>();
        services.AddScoped<IMessageHandler<AppearanceUpdateMessage>, AppearanceHandler>();
        services.AddScoped<IMessageHandler<ButtonClickMessage>, ButtonHandler>();
        services.AddScoped<IMessageHandler<EquipItemMessage>, EquipItemHandler>();
        services.AddScoped<IMessageHandler<UnequipItemMessage>, UnequipItemHandler>();
        services.AddScoped<IMessageHandler<DropItemMessage>, DropItemHandler>();
        services.AddScoped<IMessageHandler<MoveItemMessage>, MoveItemHandler>();
        services.AddScoped<IMessageHandler<NpcInteractMessage>, NpcInteractHandler>();
        services.AddScoped<IMessageHandler<ObjectInteractMessage>, ObjectInteractHandler>();
        services.AddScoped<IMessageHandler<PlayerInteractMessage>, PlayerInteractHandler>();
        services.AddScoped<IMessageHandler<DialogueContinueMessage>, DialogueContinueHandler>();
        services.AddScoped<IMessageHandler<GroundItemInteractMessage>, GroundItemInteractHandler>();
        services.AddScoped<IMessageHandler<FocusChangedMessage>, FocusChangedHandler>();
        services.AddScoped<IMessageHandler<AddFriendMessage>, AddFriendHandler>();
        services.AddScoped<IMessageHandler<RemoveFriendMessage>, RemoveFriendHandler>();
        services.AddScoped<IMessageHandler<AddIgnoreMessage>, AddIgnoreHandler>();
        services.AddScoped<IMessageHandler<RemoveIgnoreMessage>, RemoveIgnoreHandler>();
        services.AddScoped<IMessageHandler<PrivateMessageMessage>, PrivateMessageHandler>();

        return services;
    }
}
