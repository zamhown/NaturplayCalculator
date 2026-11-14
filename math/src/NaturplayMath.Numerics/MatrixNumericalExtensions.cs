using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Numerics;

public sealed class MatrixEnclosure
{
    private readonly ComplexEnclosure[,] entries;
    public int Rows => entries.GetLength(0);
    public int Columns => entries.GetLength(1);
    public ComplexEnclosure this[int row, int column] => entries[row, column];
    internal MatrixEnclosure(ComplexEnclosure[,] entries) => this.entries = (ComplexEnclosure[,])entries.Clone();
}

public static class MatrixNumericalExtensions
{
    public static MatrixEnclosure Approximate(this ExpressionMatrix matrix, ApproximationOptions options,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(matrix); ArgumentNullException.ThrowIfNull(options);
        var session = (context ?? ComputationContext.Default).Start();
        try
        {
            var result = new ComplexEnclosure[matrix.Rows, matrix.Columns];
            for (int i = 0; i < matrix.Rows; i++) for (int j = 0; j < matrix.Columns; j++)
            {
                session.Check();
                var entry = matrix[i, j];
                result[i, j] = new(
                    NumericalEvaluator.EvaluateCore(entry.Real, () => options.AbsoluteTolerance, options.MaxRefinements, variables, session),
                    NumericalEvaluator.EvaluateCore(entry.Imaginary, () => options.AbsoluteTolerance, options.MaxRefinements, variables, session));
            }
            session.Report(ComputationStage.Completed, "certified matrix enclosure", force: true);
            return new(result);
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    /// <summary>Proves every pivot decision; small intervals containing zero never count as zero.</summary>
    public static int RankCertified(this ExpressionMatrix matrix, ApproximationOptions options,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(matrix); ArgumentNullException.ThrowIfNull(options);
        context ??= ComputationContext.Default;
        var session = context.Start(); var scoped = context.Bind(session);
        bool? Zero(ComplexExpression entry)
        {
            var real = NumericalEvaluator.EvaluateCore(entry.Real, () => options.AbsoluteTolerance, options.MaxRefinements, variables, session);
            var imaginary = NumericalEvaluator.EvaluateCore(entry.Imaginary, () => options.AbsoluteTolerance, options.MaxRefinements, variables, session);
            if (!real.ContainsZero || !imaginary.ContainsZero) return false;
            return real.IsExact && imaginary.IsExact ? true : null;
        }
        try
        {
            for (int i = 0; i < matrix.Rows; i++) for (int j = 0; j < matrix.Columns; j++) Zero(matrix[i, j]);
            int result = matrix.Rank(scoped, Zero);
            session.Report(ComputationStage.Completed, "certified matrix rank", force: true);
            return result;
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    public static async Task<MatrixEnclosure> ApproximateAsync(this ExpressionMatrix matrix, ApproximationOptions options,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        context ??= ComputationContext.Default;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effective = new ComputationContext(linked.Token, context.MaxOperations, context.Progress, context.ProgressInterval);
        return await Task.Run(() => matrix.Approximate(options, snapshot, effective), linked.Token).ConfigureAwait(false);
    }
}
