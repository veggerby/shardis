using System.Reflection;

using PublicApiGenerator;

using Shardis.Query.InMemory.Execution;

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
        (typeof(ShardAssignmentResult<>).Assembly, "Shardis"),
        (typeof(Marten.MartenShard).Assembly, "Shardis.Marten"),
        (typeof(Migration.ServiceCollectionExtensions).Assembly, "Shardis.Migration"),
        (typeof(Query.Execution.IShardQueryExecutor).Assembly, "Shardis.Query"),
        (typeof(Query.EFCore.Execution.EfCoreShardQueryExecutor).Assembly, "Shardis.Query.EFCore"),
        (typeof(InMemoryShardQueryExecutor).Assembly, "Shardis.Query.InMemory"),
        (typeof(Query.Marten.AdaptiveMartenMaterializer).Assembly, "Shardis.Query.Marten"),
        (typeof(Redis.RedisShardMapStore<>).Assembly, "Shardis.Redis"),
        (typeof(Testing.Determinism).Assembly, "Shardis.Testing")
    };

    private static string ApprovedDir => Path.Combine(FindRepoRoot(), "test", "PublicApiApproval");

    [Fact]
    public void Public_api_matches_approved_baselines()
    {
        // arrange
        Directory.CreateDirectory(ApprovedDir);
        var failures = new List<string>();

        // act
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
                File.WriteAllText(approvedPath, current); // establish baseline
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

        // assert
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