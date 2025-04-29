using System.Diagnostics;

using Shardis.Querying;

namespace SampleWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IShardStreamBroadcaster<string> _broadcaster;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public Worker(ILogger<Worker> logger, IShardStreamBroadcaster<string> broadcaster, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _broadcaster = broadcaster;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var delays = new Dictionary<string, int[]>
    {
        { "session-1", [ 10, 20, 6000 ] },
        { "session-2", [ 1500, 2500, 100, 100, 1000 ] }
    };

    _logger.LogInformation("Worker starting at {time}", DateTimeOffset.Now);

    var stopwatch = Stopwatch.StartNew();

    var resultsYielded = 0;

    await foreach (var result in _broadcaster.QueryAllShardsAsync(session =>
    {
        _logger.LogInformation("Starting query for session {session}", session);

        if (delays.TryGetValue(session, out var sessionDelays))
        {
            return GetMockedResults(session, sessionDelays);
        }
        else
        {
            _logger.LogWarning("No delay config for session {session}, using default", session);
            return GetMockedResults(session);
        }
    }, stoppingToken))
    {
        resultsYielded++;
        _logger.LogInformation("Received result: {elapsed} {result}", stopwatch.Elapsed, result);
    }

    _logger.LogInformation("Total results yielded: {count}", resultsYielded);

    // Shutdown the application
    _hostApplicationLifetime.StopApplication();
}


    private async IAsyncEnumerable<string> GetMockedResults(string session, params int[] delays)
    {
        yield return $"{session}-Result0";
        int c = 1;
        foreach (var delay in delays)
        {
            await Task.Delay(delay);
            yield return $"{session}-Result{c++} +{delay}ms";
        }
    }
}
