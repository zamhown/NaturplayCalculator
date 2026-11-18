using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaturplayMath.Computation;
using NaturplayMath.Formatting;
using NaturplayMath.Numerics;
using NaturplayMath.Symbolics;
using NaturplayMath.Calculation;

int digits = args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 1000;
string outputDirectory = args.Length > 1 ? args[1] : Path.Combine("artifacts", "math-benchmarks");
if (digits < 1) throw new ArgumentOutOfRangeException(nameof(digits));
Directory.CreateDirectory(outputDirectory);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var watch = Stopwatch.StartNew();
var assemblyHashes = new[] { typeof(NaturplayMath.Numbers.Rational).Assembly, typeof(Expression).Assembly,
    typeof(NumericalEvaluator).Assembly, typeof(NumberFormatter).Assembly }
    .ToDictionary(a => a.GetName().Name!, a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))));
var progress = new ConsoleProgress();
var context = new ComputationContext(cancellation.Token, progress: progress, progressInterval: TimeSpan.FromSeconds(5));
var result = await CalculationEngine.CalculateAsync("ln(sin(1))", new(new NumberFormat(digits, trimTrailingZeros: false)), context: context);
var text = result.Text;
var evaluationTime = progress.FormattingStarted ?? watch.Elapsed;
string stem = $"ln-sin-1-decimal-{digits}";
var output = Path.Combine(outputDirectory, stem + ".txt");
await File.WriteAllTextAsync(output, text, new UTF8Encoding(false), cancellation.Token);
string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
var report = new
{
    Expression = "ln(sin(1))",
    Digits = digits,
    Radix = 10,
    Rounding = "TowardZero",
    CertifiedByBothEndpoints = true,
    EvaluationTime = evaluationTime.ToString(),
    TotalTime = watch.Elapsed.ToString(),
    PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
    Sha256 = hash,
    OutputFile = Path.GetFullPath(output),
    Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    AssemblySha256 = assemblyHashes,
    Pipeline = "exact text parse -> symbolic simplification -> certified evaluation -> certified formatting",
    Prefix = text[..Math.Min(100, text.Length)],
    Suffix = text[^Math.Min(100, text.Length)..]
};
string metadata = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(Path.Combine(outputDirectory, stem + ".json"), metadata, cancellation.Token);
Console.WriteLine(metadata);

sealed class ConsoleProgress : IProgress<ComputationProgress>
{
    public TimeSpan? FormattingStarted { get; private set; }
    public void Report(ComputationProgress value)
    {
        if (value.Stage == ComputationStage.Formatting) FormattingStarted ??= value.Elapsed;
        Console.Error.WriteLine($"{value.Elapsed:c} {value.Stage}: {value.Detail}, bits={value.WorkingPrecisionBits}, work={value.CompletedWork}/{value.TotalWork}");
    }
}
