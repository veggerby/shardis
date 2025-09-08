# Shardis Broadcaster Sample (Worker Service)

Demonstrates shard-wide broadcasting using `IShardStreamBroadcaster` inside a hosted worker.

## What it shows

* DI integration via `AddShardis` with two shards
* Asynchronous fan-out query across all shards
* Interleaved result streaming with simulated latency
* Graceful shutdown after results consumed

## Running

```bash
dotnet run --project samples/SampleWorker
```

(Logs show staggered result emission per shard session.)

## Notes

* Artificial delays simulate heterogeneous shard response times.
* Adjust delays in `Worker.cs` to experiment with streaming behavior.
