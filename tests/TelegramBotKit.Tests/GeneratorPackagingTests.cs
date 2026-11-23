using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TelegramBotKit.Commands;
using TelegramBotKit.Generators;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class GeneratorPackagingTests
{
    private const string Imports = "using System.Threading.Tasks; using Telegram.Bot.Types; using TelegramBotKit; using TelegramBotKit.Commands; ";
    private const string Body = "public Task HandleAsync(Message m, BotContext c) => Task.CompletedTask; public Task HandleAsync(CallbackQuery q, string[] a, BotContext c) => Task.CompletedTask;";
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Append(typeof(IMessageCommand).Assembly.Location)
        .Where(p => !string.Equals(p, typeof(GeneratorPackagingTests).Assembly.Location, StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)).ToArray();
    private static CSharpCompilation Input(string source, string name = "GeneratorInput", params MetadataReference[] extra) =>
        CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(Imports + source)], References.Concat(extra),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    private static (GeneratorDriver Driver, Compilation Output) Run(CSharpCompilation input)
    {
        var driver = CSharpGeneratorDriver.Create(new TelegramBotKitCommandsGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        return (driver, output);
    }
    private static string Command(string attributes, string name) => attributes + " public class " + name +
        " : IMessageCommand, ITextCommand, ICallbackCommand { " + Body + " }";

    [Fact]
    public void Existing_generator_emits_compilable_registrations_and_callback_keys()
    {
        var (driver, output) = Run(Input(Command("[MessageCommand(\"/start\")][TextCommand(\"hello\")][CallbackCommand(\"select\")]", "Command")));
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var source = driver.GetRunResult().GeneratedTrees.Single().ToString();
        Assert.Contains("AddMessageCommand<global::Command>", source);
        Assert.Contains("AddTextCommand<global::Command>", source);
        Assert.Contains("AddCallbackCommand<global::Command>", source);
        Assert.Contains("return \"select\"", source);
    }

    [Theory]
    [InlineData("[MessageCommand(\"start@Bot\")]", "[MessageCommand(\"/START\")]", "TBK001")]
    [InlineData("[CallbackCommand(\"select\")]", "[CallbackCommand(\"SELECT\")]", "TBK002")]
    [InlineData("[TextCommand(\"hello\")]", "[TextCommand(\"HELLO\")]", "TBK003")]
    public void Existing_duplicate_diagnostics_are_preserved(string first, string second, string diagnostic)
    {
        var (driver, _) = Run(Input(Command(first, "First") + Command(second, "Second")));
        Assert.Equal(diagnostic, Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Fact]
    public void Existing_public_reference_discovery_is_preserved()
    {
        var library = Input(Command("[MessageCommand(\"/library\")]", "LibraryCommand"), "CommandLibrary");
        using var stream = new MemoryStream();
        Assert.True(library.Emit(stream).Success);
        var (driver, output) = Run(Input(Command("[MessageCommand(\"/app\")]", "AppCommand"), "App",
            MetadataReference.CreateFromImage(stream.ToArray())));
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var source = driver.GetRunResult().GeneratedTrees.Single().ToString();
        Assert.Contains("AddMessageCommand<global::LibraryCommand>", source);
        Assert.Contains("AddMessageCommand<global::AppCommand>", source);
    }

    [Theory]
    [InlineData("LineFeed")]
    [InlineData("CarriageReturn")]
    [InlineData("QuotedBackslash")]
    [InlineData("TabUnicodeSeparator")]
    public void Generated_string_literals_compile_and_preserve_attribute_values(string caseName)
    {
        var value = caseName switch
        {
            "LineFeed" => "line\nfeed", "CarriageReturn" => "carriage\rreturn",
            "QuotedBackslash" => "quote\"and\\slash", _ => "tab\tvalue\u2028next"
        };
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
        var input = Input(Command("[TextCommand(" + literal + ")]", "LiteralCommand"));
        Assert.Empty(input.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var (driver, output) = Run(input);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var generated = driver.GetRunResult().GeneratedTrees.Single();
        var strings = generated.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax>()
            .Where(n => n.IsKind(SyntaxKind.StringLiteralExpression)).Select(n => n.Token.ValueText);
        Assert.Contains(value, strings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mixed_case_comparison_conflicts_are_diagnosed_in_either_declaration_order(bool exactFirst)
    {
        var exact = Command("[TextCommand(false, \"Hello\")]", "Exact");
        var ignoringCase = Command("[TextCommand(true, \"HELLO\")]", "IgnoringCase");
        var (driver, _) = Run(Input(exactFirst ? exact + ignoringCase : ignoringCase + exact));
        Assert.Equal("TBK003", Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Fact]
    public void Distinct_case_sensitive_triggers_do_not_conflict()
    {
        var (driver, output) = Run(Input(Command("[TextCommand(false, \"Hello\", \"hello\")]", "Exact")));
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
