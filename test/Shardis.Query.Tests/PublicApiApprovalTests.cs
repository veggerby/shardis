using System.Reflection;
using System.Text;

namespace Shardis.Query.Tests;

public sealed class PublicApiApprovalTests
{
    private const string ApprovedFile = "PublicApi.Shardis.Query.approved.txt";

    [Fact]
    public void PublicApi_Unchanged()
    {
        var asm = typeof(Shardis.Query.Execution.IShardQueryExecutor).Assembly;
        var sb = new StringBuilder();
        foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName))
        {
            sb.AppendLine(t.FullName);
            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                 .Where(m => m.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Event)
                                 .OrderBy(m => m.Name))
            {
                sb.AppendLine("  " + m.MemberType + " " + m.Name);
            }
        }
        var current = sb.ToString().Replace("\r\n", "\n").TrimEnd() + "\n";
        if (!File.Exists(ApprovedFile))
        {
            File.WriteAllText(ApprovedFile, current);
            return; // establish baseline first run
        }
        var approved = File.ReadAllText(ApprovedFile).Replace("\r\n", "\n").TrimEnd() + "\n";
        current.Should().Be(approved);
    }
}