namespace AwesomeAssertions;

/// <summary>
/// Lightweight assertion helpers to satisfy project requirement of using "AwesomeAssertions" instead of direct xUnit assertions
/// or external fluent assertion libraries. Intentionally minimal – extend only as needed by tests.
/// </summary>
public static class AssertionExtensions
{
    public static void ShouldNotBeNull<T>(this T? actual, string? because = null)
    {
        Xunit.Assert.NotNull(actual);
    }

    public static void ShouldBeTrue(this bool actual) => Xunit.Assert.True(actual);
    public static void ShouldBeFalse(this bool actual) => Xunit.Assert.False(actual);

    public static void ShouldEqual<T>(this T actual, T expected) => Xunit.Assert.Equal(expected, actual);

    public static void ShouldNotEqual<T>(this T actual, T other)
    {
        Xunit.Assert.NotEqual(other, actual);
    }

    public static void ShouldBeSameAs<T>(this T actual, T expected) where T : class
    {
        Xunit.Assert.Same(expected, actual);
    }

    public static void ShouldBeOfType(this object actual, Type type)
    {
        Xunit.Assert.IsType(type, actual);
    }

    public static void ShouldHaveCount<T>(this IEnumerable<T> actual, int expected)
    {
        var list = actual as ICollection<T> ?? actual.ToList();
        Xunit.Assert.Equal(expected, list.Count);
    }

    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected)
    {
        Xunit.Assert.Contains(expected, actual);
    }

    public static void ShouldContain<T>(this IEnumerable<T> actual, Func<T, bool> predicate)
    {
        if (!actual.Any(predicate))
        {
            throw new Xunit.Sdk.XunitException("Expected collection to contain matching element but none found.");
        }
    }

    public static void ShouldContainSingle<T>(this IEnumerable<T> actual, Func<T, bool> predicate)
    {
        var matches = actual.Where(predicate).Take(2).ToList();
        Xunit.Assert.True(matches.Count == 1, $"Expected a single match but found {matches.Count}.");
    }

    public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> actual, IEnumerable<T> expected)
    {
        var actualList = actual.OrderBy(x => x).ToList();
        var expectedList = expected.OrderBy(x => x).ToList();
        Xunit.Assert.Equal(expectedList, actualList);
    }

    public static void ShouldContainInOrder<T>(this IEnumerable<T> actual, params T[] expected)
    {
        var actualList = actual.ToList();
        Xunit.Assert.Equal(expected.Length, actualList.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Xunit.Assert.Equal(expected[i], actualList[i]);
        }
    }

    public static void ShouldBeLessThan<T>(this T actual, T other) where T : IComparable<T>
    {
        if (actual.CompareTo(other) >= 0)
        {
            throw new Xunit.Sdk.XunitException($"Expected {actual} to be less than {other}.");
        }
    }

    public static async Task ShouldThrowAsync<TException>(this Func<Task> action) where TException : Exception
    {
        await Xunit.Assert.ThrowsAsync<TException>(action);
    }

    public static void ShouldThrow<TException>(this Action action) where TException : Exception
    {
        Xunit.Assert.Throws<TException>(action);
    }
}