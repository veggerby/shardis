using System.Linq.Expressions;
using System.Reflection;

namespace Shardis.Query.Internals;

/// <summary>
/// Simplistic partial evaluator collapsing locally computable subtrees (closure constants etc.) for in-memory execution path.
/// Intentionally conservative to avoid altering semantics; EF Core path uses original expression for server translation.
/// </summary>
internal static class PartialEvaluator
{
    public static Expression<T> Evaluate<T>(Expression<T> expr) => (Expression<T>)new SubtreeEvaluator().Visit(expr);

    private sealed class SubtreeEvaluator : ExpressionVisitor
    {
        protected override Expression VisitInvocation(InvocationExpression node) => base.VisitInvocation(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression c)
            {
                try
                {
                    var value = GetValue(node);
                    return Expression.Constant(value, node.Type);
                }
                catch { /* fallback */ }
            }

            return base.VisitMember(node);
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Operand is ConstantExpression)
            {
                try
                {
                    var value = Expression.Lambda(node).Compile().DynamicInvoke();
                    return Expression.Constant(value, node.Type);
                }
                catch { }
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Left is ConstantExpression && node.Right is ConstantExpression)
            {
                try
                {
                    var value = Expression.Lambda(node).Compile().DynamicInvoke();
                    return Expression.Constant(value, node.Type);
                }
                catch { }
            }

            return base.VisitBinary(node);
        }

        private static object? GetValue(MemberExpression me)
        {
            switch (me.Member)
            {
                case FieldInfo fi:
                    var target = (me.Expression as ConstantExpression)?.Value;
                    return fi.GetValue(target);
                case PropertyInfo pi:
                    var t = (me.Expression as ConstantExpression)?.Value;
                    return pi.GetValue(t);
            }

            return null;
        }
    }
}