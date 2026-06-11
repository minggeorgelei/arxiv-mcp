namespace ArxivMcp.Services;

/// <summary>
/// Global gate for all outbound arXiv requests, per the arXiv API terms of use:
/// a single connection at a time, and at least <see cref="MinInterval"/> between
/// the end of one request and the start of the next. Shared by every MCP client,
/// so it must be registered as a singleton.
/// </summary>
public sealed class ArxivGate
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(3);
    private const int MaxWaiters = 10;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;
    private int _waiters;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _waiters) > MaxWaiters)
        {
            Interlocked.Decrement(ref _waiters);
            throw new ArxivUnavailableException(
                $"The arXiv request queue is full ({MaxWaiters} requests already waiting). " +
                "Please retry in a few seconds.");
        }

        try
        {
            await _lock.WaitAsync(cancellationToken);
            var penalty = MinInterval;
            try
            {
                var wait = _nextAllowed - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, cancellationToken);

                return await action(cancellationToken);
            }
            catch (ArxivRetryableException ex)
            {
                if (ex.Penalty > penalty)
                    penalty = ex.Penalty;
                throw;
            }
            finally
            {
                // Set on every path (success, failure, cancellation): failed or abandoned
                // attempts still count against arXiv's budget, and a throttled response
                // pushes _nextAllowed far enough out to pause every caller, not just this one.
                _nextAllowed = DateTimeOffset.UtcNow + penalty;
                _lock.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waiters);
        }
    }
}

/// <summary>
/// A retryable upstream failure (429/503/connection error). <see cref="Penalty"/> tells the
/// gate how long to hold back the next request — the global pause during backoff.
/// </summary>
public sealed class ArxivRetryableException(string message, TimeSpan penalty, Exception? inner = null)
    : Exception(message, inner)
{
    public TimeSpan Penalty { get; } = penalty;
}

/// <summary>Terminal failure whose message is meant to be shown to the LLM client.</summary>
public sealed class ArxivUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
