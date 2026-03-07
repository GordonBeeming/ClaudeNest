using System.Diagnostics.Metrics;

namespace ClaudeNest.Backend.Services;

public class MetricsService : BackgroundService
{
    public const string MeterName = "ClaudeNest.Backend";

    private readonly AgentTracker _agentTracker;
    private readonly ILogger<MetricsService> _logger;
    private readonly TimeProvider _timeProvider;

    // Observable gauges — OpenTelemetry scrapes these automatically
    private readonly Meter _meter;

    public MetricsService(AgentTracker agentTracker, ILogger<MetricsService> logger, TimeProvider timeProvider)
    {
        _agentTracker = agentTracker;
        _logger = logger;
        _timeProvider = timeProvider;

        _meter = new Meter(MeterName);
        _meter.CreateObservableGauge("claudenest.agents.online", () => _agentTracker.GetGlobalOnlineAgentCount(), description: "Number of online agents");
        _meter.CreateObservableGauge("claudenest.sessions.active", () => _agentTracker.GetGlobalActiveSessionCount(), description: "Number of active sessions");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the next minute boundary
        var now = _timeProvider.GetUtcNow();
        var nextMinute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(1);
        var delay = nextMinute - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), _timeProvider);

        // Log immediately on the first minute boundary, then every minute after
        do
        {
            var onlineAgents = _agentTracker.GetGlobalOnlineAgentCount();
            var activeSessions = _agentTracker.GetGlobalActiveSessionCount();

            _logger.LogInformation(
                "ClaudeNest Metrics — Agents online: {OnlineAgents}, Active sessions: {ActiveSessions}",
                onlineAgents,
                activeSessions);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }
}
