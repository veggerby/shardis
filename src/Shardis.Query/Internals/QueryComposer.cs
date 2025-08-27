using System.Linq.Expressions;

namespace Shardis.Query.Internals;

internal static class QueryComposer
{
    public static IEnumerable<TOut> ApplyEnumerable<TIn, TOut>(IEnumerable<TIn> src, QueryModel model)
    {
        IEnumerable<TIn> cur = src;
        foreach (var w in model.Where.Cast<Expression<Func<TIn, bool>>>())
        {
            cur = cur.Where(PartialEvaluator.Evaluate(w).Compile());
        }
        if (model.Select is null)
        {
            return (IEnumerable<TOut>)cur;
        }
        var sel = (Expression<Func<TIn, TOut>>)model.Select;
        return cur.Select(PartialEvaluator.Evaluate(sel).Compile());
    }

    public static IQueryable<TOut> ApplyQueryable<TIn, TOut>(IQueryable<TIn> src, QueryModel model)
    {
        IQueryable<TIn> cur = src;
        foreach (var w in model.Where.Cast<Expression<Func<TIn, bool>>>())
        {
            cur = cur.Where(w); // EF should translate
        }
        if (model.Select is null)
        {
            return (IQueryable<TOut>)cur;
        }
        var sel = (Expression<Func<TIn, TOut>>)model.Select;
        return cur.Select(sel);
    }
}