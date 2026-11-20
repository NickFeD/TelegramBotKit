# Performance measurements

Benchmark reports, source snapshots and profiling artifacts are local outputs and are not committed.

This suite measures the **local working-tree packages**, on .NET 10, without Telegram credentials or live requests. It is a permanent coverage suite, not an optimization of production behavior. BenchmarkDotNet isolates cases in separate processes and reports time, managed allocations and GC collections. Hosting also reports thread-pool work and contention.

Run from the repository root in PowerShell:

```powershell
./benchmarks/run.ps1 -Mode Validate
./benchmarks/run.ps1 -Mode Full
./benchmarks/run.ps1 -Mode Load
# Full statistical iteration count, with a 100 ms rather than 500 ms target per iteration:
./benchmarks/run.ps1 -Mode Full -IterationTimeMs 100
# Faster exploratory measurements, with less statistical confidence:
./benchmarks/run.ps1 -Mode Short -Filter '*MiddlewareBenchmarks*'
```

The runner saves SDK/runtime information, Git status/HEAD, hashes and a copy of the measured source and lock files. Results and logs are in ignored `BenchmarkDotNet.Artifacts/` directories; each run has its own directory. It requires `dotnet` and `rg`. A full run can take tens of minutes; `Validate` checks execution only, and its timings must not be used as performance evidence. Run measurements sequentially, with a quiet machine, on mains power. Do not compare measurements from different power profiles or machines as if only the code changed.

On a Windows shell that disables local scripts, use a process-scoped execution policy (this does not change the machine's policy):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File benchmarks/run.ps1 -Mode Full
```

Source snapshot files have a `.snapshot` suffix to avoid being mistaken for build inputs by BenchmarkDotNet's project discovery. Remove that suffix when restoring a snapshot to a separate checkout. The benchmark keeps the existing power plan; it does not change machine settings. Windows CPU/power information is captured separately because WMI access can be restricted. `Full` uses BDN's adaptive iteration counts with a 500 ms target by default; `-IterationTimeMs 100` keeps those counts but shortens each iteration. Record this setting when comparing runs.

For direct discovery and focused runs:

```powershell
dotnet run --project benchmarks/TelegramBotKit.Benchmarks -c Release -- --list flat
dotnet run --project benchmarks/TelegramBotKit.Benchmarks -c Release --no-build -- --filter '*CommandBenchmarks*' --noOverwrite
```

The entry point uses [BenchmarkSwitcher](https://benchmarkdotnet.org/articles/guides/how-to-run.html), forwarding CLI arguments. Never omit `--filter` for an unattended measurement. Markdown/CSV reports and full JSON retain distributions and errors. `Error` is the half-width of a 99.9% confidence interval; small differences with large errors do not establish a winner. BDN iteration percentiles are not per-request latency percentiles. The separate load harness records per-update latency.

| Area | Workload / comparison | Scope of one reported operation |
| --- | --- | --- |
| Pipeline | Direct delegate, compiled execution, composition at depths 0/1/5/20 | One call or one composition; build cost is separate from execution |
| Commands | Frozen lookup, slash with mention/argument, case-insensitive text, miss, callback with three arguments; 10/1000 commands | One lookup or parsed/routed command; no DI construction |
| Dispatch | Fallback, typed route, generic command, fluent command, callback | Full Core update, including per-update scope disposal |
| Middleware | Task/ValueTask global delegates, resolved global, inline/resolved typed route, compatibility adapter, short-circuit | Full Core update at configured depth |
| Conversations | Missing waiter, register/respond, register/cancel; 0/1000 other active waits | Complete waiter lifecycle, including cleanup; no real timeout delay |
| Hosting | Core sequential baseline vs receiver/actors; 1/64 keys, DOP 1/8, sync/yield handler | One **completed** update, normalized from a batch of 256 |
| Messaging | Raw Telegram.Bot vs facade, reply, queued sender without rate limits | One completed local HTTP round trip, including client serialization/deserialization |
| Keyboards | Callback packing, inline/reply materialization; 1/10 preconstructed rows | One construction; row/button setup excluded except callback packing |
| Startup | Generic vs fluent registration; 10/1000 commands | Fresh collection/provider, registrations, first dispatcher resolution and disposal; warm process, not process startup |
| Generator | Cold driver, same cached compilation, unrelated source edit; 10/1000 commands | One Roslyn driver run; compilation/reference preparation excluded |
| JSON | Telegram.Bot update-array deserialization, batch 1/100 | One complete JSON array, not per-update |
| Polling | Real hosted GetUpdates loop over a gated local HTTP transport; batch 1/100 | One response batch parsed and accepted by a counting receiver; Hosting isolated out |
| Webhook | Direct receiver vs authenticated TestServer POST; isolated receiver and full Core/Hosting; invalid secret | One completed accepted update, or one rejected request |

## Interpretation and limits

The scheduler batch deliberately includes enqueue loops and completion synchronization: the workload is an entire burst, with `OperationsPerInvoke = 256`. It never measures unchecked enqueue into an ever-growing queue. The direct Core baseline is sequential, so its ratio describes the cost/benefit of scheduling a burst; it is not an equal-concurrency competitor. `Yield` exercises continuation scheduling, not a realistic network delay.

Command cardinality uses the last key (`cmd9` / `cmd999`), so key length also changes; callback/slash string allocations can differ by alignment as well as cardinality. Zero-depth middleware rows are repeated controls and have identical processing behavior. `ShortCircuit` omits the terminal, so its lower time is not a replacement for a completed command. Keyboard callback packing uses the same arguments for both row-count controls; only markup materialization scales with rows.

Resolved middleware repeats the same scoped type five times: it resolves that instance five times but constructs it once per update. This isolates repeated DI lookup, and does not represent constructing five different dependency graphs. Middleware fixtures forward synchronously; async application work is represented in Hosting/load cases, not in these middleware rows. Inline Task versus ValueTask therefore compares the current adapters on synchronous forwarding, not the general merits of either return type.

The load harness separately measures 1/256 keys × DOP 1/8 × 1/4 producers × synchronous/1 ms simulated async work. Each scenario is warmed up and repeated three times. It records completed updates/s, ingress time, enqueue-to-completion p50/p95/p99/max, maximum outstanding work, peak handlers, GC collections and **process-wide** managed allocated bytes/update. Arrays and input objects are prepared before timing. Allocation includes load-driver tasks and synchronization. Delays are subject to OS timer resolution. This is a finite burst, not an open-loop arrival-rate or production soak test. Backlog is admitted attempts minus completed work, not a count of a particular internal channel. Missing completions time out and fail the run.

The completion decorator preserves `IScheduledUpdateDispatcher` and signals only after Core's dispatch has unwound and disposed the update scope. Scheduler bookkeeping can finish slightly later. This overhead is included in both batch methods. No per-update IDs are retained across batches, and active waiters are canceled during cleanup.

The local HTTP transport excludes network, Telegram server time and Telegram rate limits. Messaging includes Telegram.Bot machinery and the local response fixture; subtracting two noisy means does not accurately isolate a tiny facade cost. QueuedMessageSender is measured unchanged, with throttling disabled only in this benchmark's configuration; this does not validate its cancellation/retry/shutdown guarantees. TestServer excludes Kestrel, TLS and reverse proxies. Polling uses fixed replayed IDs and does not emulate remote offset filtering, long-poll latency or retries.

Generator setup verifies emitted source compiles. ColdGeneration is a new driver in a warmed process, not a clean build. CachedCompilation and UnrelatedSourceEdit each start from the same immutable warmed driver, avoiding progressively changing measurement state. Startup compares explicit generic and fluent registration; reflection discovery and generated registrar startup need separate equal-command fixtures before claiming either is faster.

PhotoSelector uses published packages by design. Its EF/SQLite, media browsing/export and application queries are outside these library measurements and require a separate seeded database/workload. Other uncovered behaviors include bounded-queue overload policy (not implemented), actor retirement under sustained churn, real conversation response-lane contention, retry/backoff, fault/shutdown load, and distributed/durable behavior. Public helper overloads that share an implementation are represented by workload families, not individually timed. This suite does not claim every branch has been benchmarked.

Production implementations, dependency versions and target frameworks must remain unchanged when collecting the initial baseline. The only Core accommodation is benchmark assembly access to existing internal pipeline/command types, like the existing test assembly access; no production dispatch path changes.

For optimization experiments, save a baseline run/source snapshot, change one implementation, rerun the same filters/configuration and verify behavior separately. Prioritize meaningful time or allocation improvements on real workloads. Do not choose changes from Dry results or assume a new runtime, FrozenDictionary, ValueTask or a queue will automatically help.

To compare saved full JSON reports (time ratio below 1 means the candidate is faster):

```powershell
./benchmarks/compare.ps1 -Baseline 'BenchmarkDotNet.Artifacts/<baseline-run>' -Candidate 'BenchmarkDotNet.Artifacts/<candidate-run>' |
    Export-Csv 'BenchmarkDotNet.Artifacts/comparison.csv' -NoTypeInformation
```

The comparison joins exact method/parameter names, retains errors/sample counts and rejects Dry, failed or duplicate cases. Confirm matching runtime, GC, iteration settings, power plan and input source snapshots yourself; the script cannot establish that two environments were identical. Ratios alone are not statistical significance tests.
