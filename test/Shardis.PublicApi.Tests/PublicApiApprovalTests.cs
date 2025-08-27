using System.Reflection;

using PublicApiGenerator;

using Xunit;

namespace Shardis.PublicApi.Tests;

/// <summary>
/// Public API approval test producing .approved (committed) and .received (drift) snapshots
/// for every Shardis assembly. First run creates the approved baselines automatically.
/// </summary>
public sealed class PublicApiApprovalTests
{
    private static readonly (Assembly Assembly, string Name)[] Targets =
    {
        (typeof(Shardis.ShardAssignmentResult<>).Assembly, "Shardis"),
        (typeof(Shardis.Marten.MartenShard).Assembly, "Shardis.Marten"),
        (typeof(Shardis.Migration.ServiceCollectionExtensions).Assembly, "Shardis.Migration"),
        (typeof(Shardis.Query.Execution.IShardQueryExecutor).Assembly, "Shardis.Query"),
        (typeof(Shardis.Query.Execution.EFCore.EfCoreShardQueryExecutor).Assembly, "Shardis.Query.EFCore"),
        (typeof(Shardis.Query.Execution.InMemory.InMemoryShardQueryExecutor).Assembly, "Shardis.Query.InMemory"),
        (typeof(Shardis.Query.Marten.AdaptiveMartenMaterializer).Assembly, "Shardis.Query.Marten"),
        (typeof(Shardis.Redis.RedisShardMapStore<>).Assembly, "Shardis.Redis"),
        (typeof(Shardis.Testing.Determinism).Assembly, "Shardis.Testing")
    };

    private static string ApprovedDir => Path.Combine(FindRepoRoot(), "test", "PublicApiApproval");

    [Fact]
    public void Public_api_matches_approved_baselines()
    {
        Directory.CreateDirectory(ApprovedDir);

        var failures = new List<string>();

        foreach (var (assembly, name) in Targets)
        {
            var options = new ApiGeneratorOptions
            {
                ExcludeAttributes = new[]
                {
                    "System.Runtime.Versioning.TargetFrameworkAttribute"
                }
            };

            var current = Normalize(assembly.GeneratePublicApi(options));

            var approvedPath = Path.Combine(ApprovedDir, $"PublicApi.{name}.approved.txt");
            var receivedPath = Path.Combine(ApprovedDir, $"PublicApi.{name}.received.txt");

            if (!File.Exists(approvedPath))
            {
                File.WriteAllText(approvedPath, current);
                // Establish baseline – do not fail this run.
                continue;
            }

            var approved = Normalize(File.ReadAllText(approvedPath));

            if (!string.Equals(current, approved, StringComparison.Ordinal))
            {
                File.WriteAllText(receivedPath, current);
                failures.Add($"{name} public API changed. Approved: {Relative(approvedPath)} Received: {Relative(receivedPath)}");
            }
            else if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath); // cleanup stale received file
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join("\n\n", failures) + "\nReview drift and if intentional, replace approved with received content.");
        }
    }

    private static string Normalize(string api) => api.Replace("\r\n", "\n").Trim();

    private static string Relative(string path)
    {
        var root = FindRepoRoot();
        var full = Path.GetFullPath(path);
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
        }
        return full;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shardis.sln")) || Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent!;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}