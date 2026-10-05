using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;
namespace Risk.Server.Hubs;

public sealed class InvocationLimiter : IHubFilter, IDisposable
{
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> limiters = new();

    public async ValueTask<object> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object>> next)
    {
        var limiter = limiters.GetOrAdd(context.Context.ConnectionId, _ => new(new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromSeconds(1), QueueLimit = 0, AutoReplenishment = true }));
        using var lease = limiter.AttemptAcquire();
        if (!lease.IsAcquired) throw new HubException("Too many actions. Wait a moment.");
        return await next(context);
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception exception, Func<HubLifetimeContext, Exception, Task> next)
    {
        if (limiters.TryRemove(context.Context.ConnectionId, out var limiter)) limiter.Dispose();
        await next(context, exception);
    }

    public void Dispose()
    {
        foreach (var limiter in limiters.Values) limiter.Dispose();
        limiters.Clear();
    }
}
