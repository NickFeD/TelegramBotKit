# Bundled command generation

Core ships the existing `TelegramBotKit.Generators.dll` under
`analyzers/dotnet/cs` in its NuGet package. A separate Generators PackageReference
is no longer needed. The compiler project remains separate in the repository
because it runs inside the compiler, not inside the bot process. Its target
framework and Roslyn dependency remain unchanged.

The generator's existing symbol-based discovery, generated registrations,
callback resolver and duplicate diagnostics are retained. This packaging change
does not rewrite its incremental pipeline or introduce a new module protocol.

## Removed duplication

`AddCommands()` applies generated command registrations. It no longer scans
runtime assemblies or reads command attributes through reflection.
`Keyboard.Callback<THandler>()` uses the existing generated callback resolver;
if no mapping exists, it reports a configuration error instead of reflecting
over the handler's attribute. Explicit callback keys remain supported.

Explicit generic `AddMessageCommand<T>`, `AddTextCommand<T>`,
`AddCallbackCommand<T>` and the optional Routing package's fluent helpers remain
available without generated discovery. No changes were made to pipeline,
scheduler, messaging or waiter implementations.

## Compatibility and migration

- Reference Core and remove the separate Generators PackageReference.
- Continue using `services.AddCommands()` for attributed commands.
- `AddCommands(IServiceCollection, bool fallbackToReflection = true)` retains its
  exact signature and default value for existing callers. The flag no longer
  enables reflection: `false` keeps the previous fail-fast behavior when no
  generated registrar exists; the default allows an empty application.
- The existing public SetRegistrar and SetResolver integration points are unchanged.
  Existing multi-assembly last-writer-wins limitations remain; fixing them is
  separate from combining packages.
- Source-based repository samples retain direct generator ProjectReferences
  because project references do not carry the packed analyzer asset. NuGet
  consumers receive it from Core, including through a Routing dependency.
- Removing command discovery reflection does not remove reflection inside DI or
  other dependencies and does not establish Native AOT compatibility.

No package version bump/publication has been performed. This describes the local
next-release package, not a change to already published packages. PhotoSelector
continues to use published package references by design.

## Validation

`.github/tools/validate-generated-package.ps1` packs a local Core version and runs a
consumer with only Core, then with only Routing and a transitive Core dependency.
The fixture executes slash/text/callback commands, checks generated callback keys,
and rejects network requests. It also checks that the compiler DLL is inside Core
and that Roslyn/Generators do not become application runtime dependencies.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/tools/validate-generated-package.ps1
```

An isolated workspace cache prevents an already-published cached Core package
from shadowing the newly packed version. Results are saved in an ignored
`artifacts/generation-package-*` directory. The release workflow runs the same
consumer checks against its packed package set.
