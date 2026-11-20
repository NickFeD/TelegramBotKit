using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TelegramBotKit.Commands;
using TelegramBotKit.Generators;

namespace TelegramBotKit.Benchmarks;

public class GeneratorBenchmarks
{
    [Params(10, 1000)] public int Commands { get; set; }
    private CSharpCompilation _compilation = null!, _edited = null!;
    private GeneratorDriver _warm = null!;
    [GlobalSetup] public void Setup()
    {
        var code = new StringBuilder("using TelegramBotKit.Commands; using TelegramBotKit; using Telegram.Bot.Types; using System.Threading.Tasks;\n");
        for (var i = 0; i < Commands; i++) code.Append($"[MessageCommand(\"/c{i}\")] public class Command{i} : IMessageCommand {{ public Task HandleAsync(Message m, BotContext c) => Task.CompletedTask; }}\n");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IMessageCommand).Assembly.Location).Append(typeof(Telegram.Bot.Types.Message).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p));
        _compilation = CSharpCompilation.Create("BenchmarkInput", [CSharpSyntaxTree.ParseText(code.ToString())], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        _edited = _compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("internal class UnrelatedEdit { public int Value => 42; }"));
        _warm = NewDriver().RunGenerators(_compilation);
        var result = _warm.GetRunResult();
        if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) || result.GeneratedTrees.Length == 0)
            throw new InvalidOperationException("Generator fixture produced errors or no source.");
        _warm.RunGeneratorsAndUpdateCompilation(_compilation, out var output, out var diagnostics);
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) || output.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("Generated fixture does not compile: " + string.Join("\n", output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)));
    }
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(new TelegramBotKitCommandsGenerator().AsSourceGenerator());
    [Benchmark(Baseline = true)] public GeneratorDriver ColdGeneration() => NewDriver().RunGenerators(_compilation);
    [Benchmark] public GeneratorDriver CachedCompilation() => _warm.RunGenerators(_compilation);
    [Benchmark] public GeneratorDriver UnrelatedSourceEdit() => _warm.RunGenerators(_edited);
}
