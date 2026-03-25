using System.Collections.Concurrent;
using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Manages active trade sessions and trade requests.
/// </summary>
public sealed class TradeManager
{
    // Pending trade requests: key = target username, value = requester username
    private readonly ConcurrentDictionary<string, string> _pendingRequests = new(StringComparer.OrdinalIgnoreCase);
    
    // Active trades: key = player username, value = trade session
    private readonly ConcurrentDictionary<string, TradeSession> _activeTrades = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sends a trade request from one player to another.
    /// Returns true if a mutual request was found (both players requested each other).
    /// </summary>
    public bool RequestTrade(Player requester, Player target)
    {
        // Check if the target already sent us a request
        if (_pendingRequests.TryGetValue(requester.Username, out var existingRequester) &&
            existingRequester.Equals(target.Username, StringComparison.OrdinalIgnoreCase))
        {
            // Mutual request — start trade
            _pendingRequests.TryRemove(requester.Username, out _);
            var session = new TradeSession(requester, target);
            _activeTrades[requester.Username] = session;
            _activeTrades[target.Username] = session;
            return true;
        }

        // Store our request
        _pendingRequests[target.Username] = requester.Username;
        return false;
    }

    public TradeSession? GetTrade(Player player) =>
        _activeTrades.GetValueOrDefault(player.Username);

    public void EndTrade(Player player)
    {
        if (_activeTrades.TryRemove(player.Username, out var trade))
        {
            var partner = trade.GetPartner(player);
            _activeTrades.TryRemove(partner.Username, out _);
        }
    }

    public void CancelRequest(Player player)
    {
        // Remove any pending request TO this player
        foreach (var (key, value) in _pendingRequests)
        {
            if (value.Equals(player.Username, StringComparison.OrdinalIgnoreCase))
            {
                _pendingRequests.TryRemove(key, out _);
            }
        }
    }
}
