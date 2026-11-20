# Releasing TelegramBotKit (NuGet)

The shared package version is **0.4.0-preview.1**, a SemVer prerelease.
Stable **0.4.0** is reserved for a later release. Every package inherits
`Directory.Build.props`; do not set versions in individual projects.

## Reproduce the validation locally

Install Python 3 and the exact .NET SDK **10.0.103** selected by `global.json`.
From the repository root, with an empty `artifacts/packages` directory:

```sh
python .github/tools/release.py check-version
python .github/tools/release.py build
```

The build command restores source projects, the Core test project and samples
in locked mode, builds Release, runs the Core test project, builds samples,
packs the five runtime packages, and validates the archives. The compiler project
is built as a Core dependency and is not a standalone NuGet package. A final
isolated-cache consumer run checks generated command execution with direct Core
and transitive Routing references, without a separate generator reference.
It stops on the first failure. It does not start bots or publish anything.
Existing dependency lock files must not be regenerated just to bypass a failure.

Expected output in `artifacts/packages`:

- Five `.nupkg` files: TelegramBotKit, TelegramBotKit.Hosting,
  TelegramBotKit.Polling, TelegramBotKit.Webhook.AspNetCore,
  TelegramBotKit.Routing.
- Five `.snupkg` files for the runtime packages. Core includes the generator DLL
  under `analyzers/dotnet/cs`; the generator project is built but not separately packed.
- `SHA256SUMS` covering all ten archives.

The script discovers source packages dynamically. Before publishing, select a
new version and document the [breaking polling migration](webhooks.md#polling-migration).
The working tree has not been versioned or published.

Validation checks package identities and versions, MIT license, README, icon,
repository commit, runtime DLL/XML documentation, generator analyzer layout,
internal dependency versions, portable PDBs and embedded SourceLink URLs.
To repeat only artifact validation: `python .github/tools/release.py validate`.
Review build warnings; do not globally suppress them for a release.

## CI and publication

`.github/workflows/ci.yml` runs the same build command on pushes and pull
requests and uploads package and symbol artifacts. Both workflows install
SDK 10.0.103 through `global.json`, without patch roll-forward.

`.github/workflows/publish-on-version-bump.yml` publishes on pushes to `master`
or manual `workflow_dispatch`. **Manual dispatch publishes; it is not a dry run.**
The workflow is serialized and:

1. Reads the shared version and rejects an existing version of any package on
   nuget.org or an existing release tag. Network errors fail closed.
2. Runs the full validation, then checks package availability again immediately
   before publication.
3. Authenticates and pushes packages and symbols without `--skip-duplicate`.
   Automatic symbol push is disabled for the package loop so symbols are sent once.
4. Creates `v<version>` and a GitHub Release with packages, symbols and checksums.
   Versions containing `-`, including this preview, are marked as prereleases.

A push to master without a new version will fail explicitly if that version is
already published. Never replace published contents or rebuild a different source
state under the same version. If `0.4.0-preview.1` is taken, report the conflict
before explicitly selecting `0.4.0-preview.2`. Do not silently advance versions.
NuGet publication across multiple packages is not atomic: a partial publication
requires investigation and a new version for changed sources, not duplicate skips.

Commit the reviewed release changes before publishing so SourceLink refers to
committed sources. Local validation of uncommitted changes is not a published release.

## Authentication

Set `NUGET_API_KEY` for classic authentication, or configure a nuget.org Trusted
Publishing policy for this repository and workflow file
`publish-on-version-bump.yml`, then set `NUGET_USER` to the NuGet profile name.
The API key takes precedence; otherwise `NuGet/login@v1` uses GitHub OIDC.
Neither secret is needed for local validation or ordinary CI.

## Preview scope

This preview preserves the current update pipeline, typed routes and route
middleware, single route terminals, built-in Message/CallbackQuery behavior,
commands, conversations, hosting scheduler and messaging contracts.
The release preparation changes versioning, validation and publication mechanics.

## Validation scope

The release entry point runs TelegramBotKit.Tests and the packed consumer fixture.
It does not run TelegramBotKit.Sample.PhotoSelector.Tests. PhotoSelector uses
published package references, so its tests must be run separately and do not
validate the local Core build.
