using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayMath.Computation;
namespace NaturplayMath.Symbolics;

internal static class DomainNormalizer
{
    internal static DomainCondition[] Normalize(IEnumerable<DomainCondition> source)
    {
        var retained = new SortedDictionary<string, DomainCondition>(StringComparer.Ordinal);
        void Add(Node argument, DomainRequirement requirement)
        {
            if (requirement == DomainRequirement.Defined)
            {
                if (argument.Kind is NodeKind.Constant or NodeKind.Pi or NodeKind.Symbol) return;
                var guard = new DomainCondition(argument, requirement);
                retained[guard.Key] = guard;
                return;
            }
            if (argument.Kind == NodeKind.Constant)
            {
                if ((SignBit(argument.Value.Sign) & Allowed(requirement)) == 0)
                    throw new MathDomainException("Domain condition failed: " + new DomainCondition(argument, requirement));
                return;
            }
            if (ExpressionNormalizer.IsPositive(argument)) return;
            if (requirement == DomainRequirement.Nonnegative && ExpressionNormalizer.IsNonnegative(argument)) return;
            if (requirement == DomainRequirement.NonZero && argument.Kind == NodeKind.Product)
            {
                foreach (var child in argument.Children) Add(child, DomainRequirement.NonZero);
                return;
            }
            if (argument.Kind == NodeKind.Power)
            {
                Add(argument.Children[0], requirement == DomainRequirement.NonZero || argument.Exponent.IsEven
                    ? DomainRequirement.NonZero : requirement == DomainRequirement.Positive || argument.Exponent.Sign < 0
                    ? DomainRequirement.Positive : DomainRequirement.Nonnegative);
                return;
            }
            if (argument.Kind == NodeKind.Abs)
            {
                Add(argument.Children[0], DomainRequirement.NonZero);
                return;
            }
            if (argument.Kind == NodeKind.Sqrt)
            {
                Add(argument.Children[0], DomainRequirement.Positive);
                return;
            }
            var condition = new DomainCondition(argument, requirement);
            retained[condition.Key] = condition;
        }
        foreach (var condition in source) Add(condition.ArgumentNode, condition.Requirement);
        foreach (var condition in retained.Values.ToArray())
        {
            var key = condition.ArgumentNode.Key;
            if (condition.Requirement == DomainRequirement.Nonnegative && retained.ContainsKey(DomainRequirement.NonZero + ":" + key))
            {
                var positive = new DomainCondition(condition.ArgumentNode, DomainRequirement.Positive);
                retained[positive.Key] = positive;
            }
        }
        foreach (var condition in retained.Values.Where(c => c.Requirement == DomainRequirement.Positive).ToArray())
        {
            retained.Remove(DomainRequirement.NonZero + ":" + condition.ArgumentNode.Key);
            retained.Remove(DomainRequirement.Nonnegative + ":" + condition.ArgumentNode.Key);
        }
        // Remove only conditions proven by the remaining ones, never by themselves.
        foreach (var condition in retained.Values.ToArray())
        {
            if (condition.Requirement == DomainRequirement.Defined) continue;
            var knowledge = retained.Values.Where(c => c.Key != condition.Key)
                .Where(c => c.Requirement != DomainRequirement.Defined)
                .ToDictionary(c => c.ArgumentNode.Key, c => Allowed(c.Requirement), StringComparer.Ordinal);
            var signs = Signs(condition.ArgumentNode, knowledge, new Dictionary<string, int>(StringComparer.Ordinal));
            if (signs != 0 && (signs & ~Allowed(condition.Requirement)) == 0) retained.Remove(condition.Key);
        }
        return retained.Values.ToArray();
    }
    // These flags describe possible signs; they never represent a mathematical magnitude.
    private static int Allowed(DomainRequirement requirement) => requirement switch
    { DomainRequirement.NonZero => 1 | 4, DomainRequirement.Nonnegative => 2 | 4, _ => 4 };
    private static int SignBit(int sign) => sign < 0 ? 1 : sign == 0 ? 2 : 4;
    private static int Signs(Node node, IReadOnlyDictionary<string, int> knowledge, Dictionary<string, int> cache)
    {
        if (cache.TryGetValue(node.Key, out int cached)) return cached;
        int result;
        switch (node.Kind)
        {
            case NodeKind.Constant: result = SignBit(node.Value.Sign); break;
            case NodeKind.Exp:
            case NodeKind.Pi: result = 4; break;
            case NodeKind.Abs:
                var child = Signs(node.Children[0], knowledge, cache);
                result = (child & 2) | ((child & 5) != 0 ? 4 : 0); break;
            case NodeKind.Sqrt:
                var radicand = Signs(node.Children[0], knowledge, cache);
                result = radicand == 4 ? 4 : 2 | 4; break;
            case NodeKind.Power:
                var basis = Signs(node.Children[0], knowledge, cache);
                result = node.Exponent.IsEven ? (basis & 2) | ((basis & 5) != 0 ? 4 : 0) : basis;
                if (node.Exponent.Sign < 0) result &= ~2;
                break;
            case NodeKind.Sum:
            case NodeKind.Product:
                result = node.Kind == NodeKind.Sum ? 2 : 4;
                foreach (var factor in node.Children) result = Combine(result, Signs(factor, knowledge, cache), node.Kind == NodeKind.Sum);
                break;
            default: result = 1 | 2 | 4; break;
        }
        if (knowledge.TryGetValue(node.Key, out int known)) result &= known;
        cache[node.Key] = result;
        return result;
    }
    private static int Combine(int left, int right, bool add)
    {
        int result = 0;
        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
        {
            if ((left & SignBit(a)) == 0 || (right & SignBit(b)) == 0) continue;
            result |= add && a * b < 0 ? 7 : SignBit(add ? a + b : a * b);
        }
        return result;
    }
}
