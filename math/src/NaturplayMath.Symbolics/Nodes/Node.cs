using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
namespace NaturplayMath.Symbolics;

// Nodes and their children are private to the assembly and never modified after construction.
internal sealed class Node
{
    internal NodeKind Kind { get; }
    internal Rational Value { get; }
    internal string Name { get; }
    internal BigInteger Exponent { get; }
    internal IReadOnlyList<Node> Children { get; }
    internal string Key { get; }
    private Node(NodeKind kind, Rational value, string? name, BigInteger exponent, params Node[] children)
    {
        Kind = kind; Value = value; Name = name ?? string.Empty; Exponent = exponent;
        Children = Array.AsReadOnly(children);
        Key = kind switch
        {
            NodeKind.Constant => "C(" + value + ")",
            NodeKind.Symbol => "V(" + Name.Length + ":" + Name + ")",
            NodeKind.Power => "P(" + exponent.ToString(CultureInfo.InvariantCulture) + ":" + children[0].Key + ")",
            NodeKind.Root => "R(" + exponent.ToString(CultureInfo.InvariantCulture) + ":" + children[0].Key + ")",
            _ => kind + "(" + string.Join(",", children.Select(c => c.Key)) + ")"
        };
    }
    internal static Node Constant(Rational value) => new(NodeKind.Constant, value, null, 0);
    internal static Node Symbol(string name) => new(NodeKind.Symbol, default, name, 0);
    internal static Node Make(NodeKind kind, params Node[] children) => new(kind, default, null, 0, children);
    internal static Node Power(Node child, BigInteger exponent) => new(NodeKind.Power, default, null, exponent, child);
    internal static Node Root(Node child, BigInteger degree) => new(NodeKind.Root, default, null, degree, child);
}

