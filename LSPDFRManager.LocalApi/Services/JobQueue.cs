using System.Collections.Concurrent;

namespace LSPDFRManager.LocalApi.Services;

public sealed class JobEntry
{
    public required string JobId { get; init; }
    public string State { get; set; } = "Pending";
    public int ProgressPct { get; set; }
    public string? Error { get; set; }
    public string? ResultJson { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // A detached copy handed to readers so they never observe a field being
    // mutated by a background thread mid-read.
    internal JobEntry Snapshot() => new()
    {
        JobId = JobId,
        State = State,
        ProgressPct = ProgressPct,
        Error = Error,
        ResultJson = ResultJson,
        StartedAt = StartedAt,
        CompletedAt = CompletedAt,
    };
}

public sealed class JobQueue : IDisposable
{
    private readonly ConcurrentDictionary<string, JobEntry> _jobs = new();
    private readonly object _lock = new();
    private readonly Timer _pruner;

    public JobQueue()
    {
        // Prune completed/failed/cancelled jobs older than 10 minutes, every 2 minutes.
        _pruner = new Timer(_ => Prune(), null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
    }

    public string CreateJob()
    {
        var jobId = Guid.NewGuid().ToString("N");
        lock (_lock)
            _jobs[jobId] = new JobEntry { JobId = jobId };
        return jobId;
    }

    public void UpdateProgress(string jobId, int pct, string state)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.ProgressPct = pct;
                job.State = state;
            }
        }
    }

    public void CompleteJob(string jobId, string? resultJson = null)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.State = "Completed";
                job.ProgressPct = 100;
                job.ResultJson = resultJson;
                job.CompletedAt = DateTime.UtcNow;
            }
        }
    }

    public void FailJob(string jobId, string? error)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.State = "Failed";
                job.Error = error;
                job.CompletedAt = DateTime.UtcNow;
            }
        }
    }

    public JobEntry? GetJob(string jobId)
    {
        lock (_lock)
            return _jobs.TryGetValue(jobId, out var job) ? job.Snapshot() : null;
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        lock (_lock)
        {
            foreach (var kv in _jobs)
            {
                var job = kv.Value;
                var isTerminal = job.State is "Completed" or "Failed" or "Cancelled";
                if (isTerminal && job.CompletedAt.HasValue && job.CompletedAt.Value < cutoff)
                    _jobs.TryRemove(kv.Key, out _);
            }
        }
    }

    public void Dispose() => _pruner.Dispose();
}
