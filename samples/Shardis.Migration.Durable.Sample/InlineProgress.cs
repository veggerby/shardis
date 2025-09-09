using Shardis.Migration.Execution;

namespace Shardis.Migration.Durable.Sample;

internal sealed class InlineProgress : IProgress<MigrationProgressEvent>
{
    public MigrationProgressEvent? Summary { get; private set; }
    public void Report(MigrationProgressEvent value)
    {
        Summary = value;
        if ((value.Copied + value.Verified + value.Swapped) % 25 == 0)
        {
            Console.WriteLine($"Copied={value.Copied}/{value.Total} Verified={value.Verified} Swapped={value.Swapped}");
        }
    }
}