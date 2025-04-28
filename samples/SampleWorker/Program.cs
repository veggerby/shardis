using SampleWorker;

using Shardis;
using Shardis.Model;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddShardis<SimpleShard, string>(options =>
{
    options.Shards.Add(new SimpleShard(new("Shard1"), "session-1"));
    options.Shards.Add(new SimpleShard(new("Shard2"), "session-2"));
});

var host = builder.Build();
host.Run();
