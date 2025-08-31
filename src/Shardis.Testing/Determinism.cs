namespace Shardis.Testing;

/// <summary>
/// Provides seeded deterministic helpers for benchmarks and tests: RNG, delay schedules, shuffles and gating primitives.
/// </summary>
public sealed class Determinism
{
    private Determinism(int seed)
    {
        Rng = new Random(seed);
    }

    /// <summary>Creates a new seeded determinism context.</summary>
    public static Determinism Create(int seed) => new(seed);

    /// <summary>The underlying seeded RNG (not thread-safe; guard externally if shared across threads).</summary>
    public Random Rng { get; }

    /// <summary>
    /// Generates per-shard delay schedules according to skew profile.
    /// </summary>
    /// <param name="shards">Number of shards.</param>
    /// <param name="skew">Skew intensity.</param>
    /// <param name="baseDelay">Base delay per step.</param>
    /// <param name="steps">Number of delay steps to precompute.</param>
    /// <param name="jitter">Optional +/- fractional jitter (0..1) applied deterministically.</param>
    public TimeSpan[][] MakeDelays(int shards, Skew skew, TimeSpan baseDelay, int steps = 256, double jitter = 0.0)
    {
        if (shards <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shards));
        }

        if (steps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps));
        }

        if (jitter is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(jitter));
        }

        var scales = skew switch
        {
            Skew.None => Enumerable.Repeat(1.0, shards).ToArray(),
            Skew.Mild => SkewProfile(shards, 1.0, 3.0).ToArray(),
            Skew.Harsh => SkewProfile(shards, 1.0, 10.0).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(skew))
        };

        var schedules = new TimeSpan[shards][];
        for (int s = 0; s < shards; s++)
        {
            var arr = new TimeSpan[steps];

            for (int i = 0; i < steps; i++)
            {
                arr[i] = Jitter(baseDelay, scales[s], jitter);
            }

            schedules[s] = arr;
        }

        return schedules;
    }

    /// <summary>Delays using a precomputed schedule for a shard and logical step.</summary>
    public Task DelayForShardAsync(TimeSpan[][] schedules, int shardIndex, int step, CancellationToken ct = default)
    {
        var schedule = schedules[shardIndex];
        var delay = schedule[step % schedule.Length];
        return Task.Delay(delay, ct);
    }

    /// <summary>Deterministically shuffles an array in-place (Fisher-Yates) and returns it.</summary>
    public T[] ShuffleStable<T>(T[] items)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    /// <summary>Generates a deterministic sequence using the supplied factory.</summary>
    public IEnumerable<T> Generate<T>(Func<Random, T> factory, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return factory(Rng);
        }
    }

    /// <summary>Creates a gate for explicit interleaving control.</summary>
    public (Func<Task> WaitAsync, Action Release) Gate()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return (async () => await tcs.Task.ConfigureAwait(false), () => tcs.TrySetResult());
    }

    private static IEnumerable<double> SkewProfile(int shards, double min, double max)
    {
        if (shards == 1)
        {
            yield return min;
            yield break;
        }

        var step = (max - min) / (shards - 1);

        for (int i = 0; i < shards; i++)
        {
            yield return min + i * step;
        }
    }

    private TimeSpan Jitter(TimeSpan baseDelay, double scale, double jitter)
    {
        if (jitter <= 0)
        {
            return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * scale);
        }

        var delta = (Rng.NextDouble() * 2 - 1) * jitter; // [-jitter, +jitter]
        var ms = baseDelay.TotalMilliseconds * scale * (1.0 + delta);

        return TimeSpan.FromMilliseconds(ms);
    }
}