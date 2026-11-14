using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Numerics;

public static class NumericalEvaluator
{
    public static Task<RealInterval> EvaluateAsync(Expression expression, ApproximationOptions options,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return RunAsync(effective => Evaluate(expression, options, snapshot, effective), context, cancellationToken);
    }
    internal static async Task<RealInterval> RunAsync(Func<ComputationContext, RealInterval> operation,
        ComputationContext? context, CancellationToken cancellationToken)
    {
        context ??= ComputationContext.Default;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effective = new ComputationContext(linked.Token, context.MaxOperations, context.Progress, context.ProgressInterval);
        return await Task.Run(() => operation(effective), linked.Token).ConfigureAwait(false);
    }
    /// <summary>Simplifies substitutions first, then encloses the value within the requested absolute width.</summary>
    public static RealInterval Evaluate(Expression expression, ApproximationOptions options,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Evaluate(expression, () => options.AbsoluteTolerance, options.MaxRefinements, variables,
            (context ?? ComputationContext.Default).Start());
    }
    internal static RealInterval Evaluate(Expression expression, Func<Rational> targetFactory, int maxRefinements,
        IReadOnlyDictionary<string, Rational>? variables, ComputationSession session)
    {
        try
        {
            session.Report(ComputationStage.Simplifying, "exact substitution and simplification", force: true);
            var result = EvaluateCore(expression, targetFactory, maxRefinements, variables, session);
            session.Report(ComputationStage.Completed, "certified result", force: true);
            return result;
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    internal static RealInterval EvaluateCore(Expression expression, Func<Rational> targetFactory, int maxRefinements,
        IReadOnlyDictionary<string, Rational>? variables, ComputationSession session)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (variables is not null) expression = expression.Substitute(variables.ToDictionary(p => p.Key, p => Expression.Constant(p.Value), StringComparer.Ordinal), session);
        if (expression.TryGetRational(out var exact)) return RealInterval.Exact(exact);
        var target = targetFactory();
        int precision = checked((int)Math.Max(128, target.Denominator.GetBitLength() - target.Numerator.GetBitLength() + 64));
        // The logarithm's AGM enclosure initially yields about 2/3 of the working bits.
        // This is only a starting estimate; the final enclosure is still checked against the target.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool UsesLogarithm(Node node)
        {
            if (!visited.Add(node.Key)) return false;
            return node.Kind is NodeKind.Ln or NodeKind.Asinh or NodeKind.GeneralPower or NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag || node.Kind == NodeKind.Root && node.Exponent > 16
                || node.Children.Any(UsesLogarithm);
        }
        if (UsesLogarithm(expression.Root) || expression.Conditions.Any(c => UsesLogarithm(c.ArgumentNode))) precision = checked(precision + precision / 2 + 64);
        int? inputResolution = null;
        int InputResolution()
        {
            if (inputResolution.HasValue) return inputResolution.Value;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long Resolution(Node node)
            {
                if (!seen.Add(node.Key)) return 0;
                if (node.Kind == NodeKind.Constant) return Math.Max(node.Value.Numerator.GetBitLength(), node.Value.Denominator.GetBitLength());
                long bits = 0;
                foreach (var child in node.Children) bits = Math.Max(bits, Resolution(child));
                return bits;
            }
            long result = Resolution(expression.Root);
            foreach (var condition in expression.Conditions) result = Math.Max(result, Resolution(condition.ArgumentNode));
            inputResolution = checked((int)result + 64);
            return inputResolution.Value;
        }
        for (int attempt = 0; attempt < maxRefinements; attempt++)
        {
            session.Check();
            session.Report(attempt == 0 ? ComputationStage.Evaluating : ComputationStage.Refining,
                "expression enclosure", precision, attempt, maxRefinements, true);
            var kernel = new BinaryKernel(precision, session);
            var cache = new Dictionary<string, BinaryInterval>(StringComparer.Ordinal);
            try
            {
                BinaryInterval Visit(Node node)
                {
                    session.Check();
                    if (cache.TryGetValue(node.Key, out var cached)) return cached;
                    var args = node.Children.Select(Visit).ToArray();
                    BinaryInterval result;
                    switch (node.Kind)
                    {
                        case NodeKind.Constant: result = kernel.From(node.Value); break;
                        case NodeKind.Symbol: throw new UnboundSymbolException(node.Name);
                        case NodeKind.Pi: result = kernel.Pi(); break;
                        case NodeKind.Sum:
                            result = kernel.Zero;
                            foreach (var arg in args) { session.Check(); result += arg; }
                            break;
                        case NodeKind.Product:
                            result = kernel.One;
                            foreach (var arg in args) { session.Check(); result *= arg; }
                            break;
                        case NodeKind.Power:
                            if (node.Exponent.Sign <= 0) Require(args[0], DomainRequirement.NonZero);
                            result = kernel.Pow(args[0], node.Exponent); break;
                        case NodeKind.Sqrt: Require(args[0], DomainRequirement.Nonnegative); result = kernel.Sqrt(args[0]); break;
                        case NodeKind.Root: result = kernel.Root(args[0], node.Exponent); break;
                        case NodeKind.GeneralPower:
                            if (args[0].Lower.IsZero && args[0].Upper.IsZero)
                            {
                                Require(args[1], DomainRequirement.Positive); result = kernel.Zero;
                            }
                            else { Require(args[0], DomainRequirement.Positive); result = kernel.Exp(args[1] * kernel.Ln(args[0])); }
                            break;
                        case NodeKind.Ln: Require(args[0], DomainRequirement.Positive); result = kernel.Ln(args[0]); break;
                        case NodeKind.Exp: result = kernel.Exp(args[0]); break;
                        case NodeKind.Abs: result = args[0].Abs(); break;
                        case NodeKind.Sin: result = kernel.SinCos(args[0]).Sin; break;
                        case NodeKind.Cos: result = kernel.SinCos(args[0]).Cos; break;
                        case NodeKind.Atan: result = kernel.Atan(args[0]); break;
                        case NodeKind.Asin: result = kernel.Asin(args[0]); break;
                        case NodeKind.Asinh: result = kernel.Asinh(args[0]); break;
                        case NodeKind.Tanh: result = kernel.Tanh(args[0]); break;
                        case NodeKind.Atan2: result = kernel.Atan2(args[0], args[1]); break;
                        case NodeKind.ComplexRootReal: result = kernel.ComplexSqrt(args[0], args[1]).Real; break;
                        case NodeKind.ComplexRootImag: result = kernel.ComplexSqrt(args[0], args[1]).Imaginary; break;
                        case NodeKind.ComplexPowerReal: result = kernel.ComplexPow(args[0], args[1], args[2], args[3]).Real; break;
                        case NodeKind.ComplexPowerImag: result = kernel.ComplexPow(args[0], args[1], args[2], args[3]).Imaginary; break;
                        case NodeKind.Floor:
                        case NodeKind.Ceiling:
                        case NodeKind.Truncate:
                            BigInteger Round(BigInteger units) => node.Kind == NodeKind.Floor ? units >> precision
                                : node.Kind == NodeKind.Ceiling ? -((-units) >> precision) : units / kernel.Scale;
                            var low = Round(args[0].Lower); var high = Round(args[0].Upper);
                            if (low != high) throw new RefinementNeededException();
                            result = kernel.From(new Rational(low)); break;
                        case NodeKind.Factorial:
                        case NodeKind.DoubleFactorial:
                        case NodeKind.Choose:
                        case NodeKind.Permute:
                            BigInteger Integer(BinaryInterval arg)
                            {
                                var lowerInteger = -((-arg.Lower) >> precision);
                                var upperInteger = arg.Upper >> precision;
                                if (lowerInteger > upperInteger) throw new MathDomainException("This operation requires integer arguments.");
                                if (arg.Width != 0) throw new RefinementNeededException();
                                return lowerInteger;
                            }
                            var n = Integer(args[0]);
                            var value = node.Kind switch
                            {
                                NodeKind.Factorial => Combinatorics.Factorial(n, false, session),
                                NodeKind.DoubleFactorial => Combinatorics.Factorial(n, true, session),
                                NodeKind.Choose => Combinatorics.Choose(n, Integer(args[1]), session),
                                _ => Combinatorics.Permute(n, Integer(args[1]), session)
                            };
                            result = kernel.From(new Rational(value)); break;
                        default: throw new InvalidOperationException("Unknown expression node.");
                    }
                    cache[node.Key] = result; return result;
                }
                foreach (var condition in expression.Conditions) Require(Visit(condition.ArgumentNode), condition.Requirement);
                var result = Visit(expression.Root);
                var achieved = result.Width * target.Denominator;
                var requested = target.Numerator << precision;
                if (achieved <= requested) return result.ToRational();
                int missing = checked((int)Math.Max(32, achieved.GetBitLength() - requested.GetBitLength() + 16));
                precision = checked(precision + 2 * missing);
            }
            catch (RefinementNeededException error)
            {
                precision = Math.Max(checked(precision + precision / 2 + 64), Math.Max(InputResolution(), error.RequiredPrecision ?? 0));
            }
        }
        throw new InsufficientPrecisionException("Could not certify the domain and requested error bound within MaxRefinements; increase it or simplify the expression.");
    }
    private static void Require(BinaryInterval value, DomainRequirement requirement)
    {
        if (requirement == DomainRequirement.Defined) return;
        bool valid = requirement switch
        {
            DomainRequirement.NonZero => !value.ContainsZero,
            DomainRequirement.Nonnegative => value.Lower.Sign >= 0,
            _ => value.Lower.Sign > 0
        };
        if (valid) return;
        bool invalid = requirement switch
        {
            DomainRequirement.NonZero => value.Lower.IsZero && value.Upper.IsZero,
            DomainRequirement.Nonnegative => value.Upper.Sign < 0,
            _ => value.Upper.Sign <= 0
        };
        if (invalid) throw new MathDomainException("The expression is outside its real domain.");
        throw new RefinementNeededException();
    }
}
