using System.Reflection;
using System.Text;
using Shardis.Query.Execution;

var asm = typeof(IShardQueryExecutor).Assembly;
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
File.WriteAllText("APIDUMP.txt", sb.ToString());
Console.WriteLine(sb.ToString());
