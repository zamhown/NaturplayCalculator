using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Model
{
    internal interface ICalculationDomain<T>
    {
        T Number(string text);
        T Symbol(string name);
        T Negate(T value);
        T Binary(string operation, T left, T right);
        T Function(string name, T argument);
        T Matrix(int rows, int columns, IReadOnlyList<T> cells);
        ExpressionNode ToExpression(T value);
    }

    // One traversal and one step mechanism for numeric and exact arithmetic.
    internal sealed class ExpressionEvaluation<T>
    {
        private readonly ICalculationDomain<T> domain;
        private readonly ExpressionNode root;
        private readonly Dictionary<ExpressionNode, ExpressionNode> reduced = new Dictionary<ExpressionNode, ExpressionNode>();
        private readonly List<ExpressionNode> steps = new List<ExpressionNode>();
        private readonly int maxSteps;
        internal IReadOnlyList<ExpressionNode> Steps => steps.AsReadOnly();
        internal ExpressionEvaluation(ICalculationDomain<T> domain, ExpressionNode root, int maxSteps = 0)
        { this.domain = domain; this.root = root; this.maxSteps = maxSteps; }
        internal T Evaluate() => Visit(root);
        private T Visit(ExpressionNode node)
        {
            CalculationContext.Check();
            T value;
            switch (node.Kind)
            {
                case ExpressionKind.Number: value = domain.Number(node.Text); break;
                case ExpressionKind.Symbol: value = domain.Symbol(node.Text); break;
                case ExpressionKind.Unary: value = domain.Negate(Visit(node.Children[0])); break;
                case ExpressionKind.Binary: value = domain.Binary(node.Text, Visit(node.Children[0]), Visit(node.Children[1])); break;
                case ExpressionKind.Function: value = domain.Function(node.Text, Visit(node.Children[0])); break;
                case ExpressionKind.Matrix: value = domain.Matrix(node.Rows, node.Columns, node.Children.Select(Visit).ToArray()); break;
                default: throw new InvalidOperationException();
            }
            if (steps.Count < maxSteps && node.Kind != ExpressionKind.Number)
            {
                reduced[node] = domain.ToExpression(value);
                var snapshot = Snapshot(root);
                if (snapshot.Key != root.Key && (steps.Count == 0 || steps[steps.Count - 1].Key != snapshot.Key)) steps.Add(snapshot);
            }
            return value;
        }
        private ExpressionNode Snapshot(ExpressionNode node)
        {
            if (reduced.TryGetValue(node, out var replacement)) return replacement;
            return node.Children.Count == 0 ? node : node.WithChildren(node.Children.Select(Snapshot));
        }
    }
}
