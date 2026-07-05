using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MPM.Modules.Mensajeria.Hubs;

[Authorize]
public class MensajeriaHub : Hub
{
    public async Task UnirseConversacion(long conversacionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversacion_{conversacionId}");
    }

    public async Task SalirConversacion(long conversacionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversacion_{conversacionId}");
    }

    public async Task NotificarTyping(long conversacionId, bool escribiendo)
    {
        var userId = Context.UserIdentifier;
        var userName = Context.User?.Identity?.Name ?? "Usuario";

        await Clients.Group($"conversacion_{conversacionId}").SendAsync("TypingIndicator", new
        {
            conversacionId,
            userId,
            userName,
            escribiendo
        });
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.All.SendAsync("PresenceUpdate", new
            {
                userId,
                estado = "online",
                updatedAt = DateTime.UtcNow
            });
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.All.SendAsync("PresenceUpdate", new
            {
                userId,
                estado = "offline",
                updatedAt = DateTime.UtcNow
            });
        }
        await base.OnDisconnectedAsync(exception);
    }
}
