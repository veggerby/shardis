using Xunit;

namespace Shardis.Tests;

/// <summary>
/// Tests to ensure no sync-over-async patterns exist in the codebase.
/// </summary>
public class SyncOverAsyncTests
{
    [Fact]
    public void No_GetAwaiter_GetResult_In_Source_Code()
    {
        // arrange
        var sourceDirectory = Path.Combine(GetRepositoryRoot(), "src");
        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

        // act
        var violations = new List<string>();
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Skip comments
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//")) continue;
                
                // Check for .GetAwaiter().GetResult()
                if (line.Contains(".GetAwaiter().GetResult()"))
                {
                    violations.Add($"{file}:{i + 1} - {line.Trim()}");
                }
            }
        }

        // assert
        Assert.Empty(violations);
    }

    [Fact]
    public void No_Task_Result_In_Source_Code()
    {
        // arrange
        var sourceDirectory = Path.Combine(GetRepositoryRoot(), "src");
        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

        // act
        var violations = new List<string>();
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Skip comments
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//")) continue;
                
                // Skip legitimate uses (e.g., Task<TResult>, returning Result properties in data classes)
                if (trimmed.Contains("Task<") || trimmed.Contains("ValueTask<")) continue;
                if (trimmed.Contains("record struct") || trimmed.Contains("class ") || trimmed.Contains("interface ")) continue;
                
                // Check for Task.Result but exclude legitimate completed task access after await
                // The only acceptable use is accessing .Result on a task that is already awaited (e.g., after Task.WhenAll)
                if (line.Contains(".Result") && 
                    (line.Contains("Task[") || line.Contains("firstFetchTasks[") || line.Contains("tasks[")))
                {
                    // This is likely accessing a completed task from an array after Task.WhenAll
                    // Example: var r = firstFetchTasks[i].Result; // already completed
                    if (!line.Contains("// already completed") && !line.Contains("// safe: already completed"))
                    {
                        violations.Add($"{file}:{i + 1} - {line.Trim()} (add '// already completed' comment if this is safe)");
                    }
                    continue;
                }
                
                // Check for other .Result patterns
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\)\s*\.Result\b") || 
                    System.Text.RegularExpressions.Regex.IsMatch(line, @"\w+\.Result\b"))
                {
                    // Skip if it's a property definition or not a Task result
                    if (!trimmed.Contains("{ get") && !trimmed.Contains("=>") && !trimmed.StartsWith("public") && !trimmed.StartsWith("private"))
                    {
                        violations.Add($"{file}:{i + 1} - {line.Trim()}");
                    }
                }
            }
        }

        // assert
        Assert.Empty(violations);
    }

    [Fact]
    public void No_Task_Wait_In_Source_Code()
    {
        // arrange
        var sourceDirectory = Path.Combine(GetRepositoryRoot(), "src");
        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

        // act
        var violations = new List<string>();
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Skip comments
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//")) continue;
                
                // Check for .Wait(
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\.Wait\s*\("))
                {
                    violations.Add($"{file}:{i + 1} - {line.Trim()}");
                }
            }
        }

        // assert
        Assert.Empty(violations);
    }

    private static string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir, ".git")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find repository root");
    }
}
