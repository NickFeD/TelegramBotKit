using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using TelegramBotKit.Benchmarks;

if (args.Contains("--load", StringComparer.Ordinal))
{
    await SchedulerLoad.RunAsync();
    return;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,
    DefaultConfig.Instance.AddDiagnoser(MemoryDiagnoser.Default).AddExporter(JsonExporter.Full)
        .AddJob(Job.Default.DontEnforcePowerPlan().AsMutator()));
var failed = summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success));
Console.WriteLine($"Measurement status: {(failed ? "FAILED" : "OK")}; reports: {summaries.Sum(s => s.Reports.Length)}");
Environment.ExitCode = failed ? 1 : 0;
