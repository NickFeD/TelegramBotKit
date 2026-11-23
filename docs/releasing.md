# Releasing TelegramBotKit (NuGet)

The prepared stable version is **0.4.0**, set in `Directory.Build.props`.
Every package inherits this property; do not set versions in individual projects. Consumer changes are covered
by the [0.3.2 → 0.4.0 migration guide](migration-0.3.2-to-0.4.0.md).

## Reproduce the validation locally

Install Python 3 and .NET SDK **10.0.404**, pinned in `global.json`.
Roll-forward is disabled so local builds and CI select the same SDK.
From the repository root, with an empty `artifacts/packages` directory:

```sh
python .github/tools/release.py check-version
python .github/tools/release.py build
```

The build command rejects credential-like values in tracked configuration files,
restores source projects, all discovered test projects and samples
in locked mode, builds Release with warnings treated as errors, runs both test projects, builds samples,
packs the five runtime packages, and validates the archives. The compiler project
is built as a Core dependency and is not a standalone NuGet package. A final
isolated-cache consumer run checks generated command execution with direct Core
and transitive Routing references, without a separate generator reference.
It also checks that all four examples reject missing tokens before starting delivery.
These checks use temporary configuration directories and do not contact Telegram.
It stops on the first failure and does not publish anything.
Existing dependency lock files must not be regenerated just to bypass a failure.

Expected output in `artifacts/packages`:

- Five `.nupkg` files: TelegramBotKit, TelegramBotKit.Hosting,
  TelegramBotKit.Polling, TelegramBotKit.Webhook.AspNetCore,
  TelegramBotKit.Routing.
- Five `.snupkg` files for the runtime packages. Core includes the generator DLL
  under `analyzers/dotnet/cs`; the generator project is built but not separately packed.
- `SHA256SUMS` covering all ten archives.

The script discovers source packages dynamically. Before publishing, review the
[migration guide](migration-0.3.2-to-0.4.0.md).
Documentation changes alone do not update or publish packages.

For a separate validation run, select a new empty output directory with
`python .github/tools/release.py build --output artifacts/release-check`.
Existing output is preserved and rejected rather than overwritten.

Validation checks package identities and versions, MIT license, README, icon,
repository commit, runtime DLL/XML documentation, generator analyzer layout,
internal dependency versions, portable PDBs and embedded SourceLink URLs.
To repeat only artifact validation: `python .github/tools/release.py validate`.
Review build warnings; do not globally suppress them for a release.

## CI and publication

`.github/workflows/ci.yml` runs the same build command on pushes and pull
requests and uploads package and symbol artifacts. Both workflows install
SDK 10.0.404 through `global.json` with roll-forward disabled.

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
   Versions containing `-` are marked as prereleases; `0.4.0` is a stable release.

A push to master without a new version will fail explicitly if that version is
already published. Never replace published contents or rebuild a different source
state under the same version. If the selected version is taken, report the conflict
before explicitly selecting another version. Do not silently advance versions.
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

## 0.4.0 scope

Compared with 0.3.2, this release introduces typed update routes with route-local
middleware and a single terminal, moves polling to its own package, adds the
ASP.NET Core webhook adapter and bundles command generation with Core.
Runtime reflection command discovery is removed. It also changes middleware
lifetimes, strengthens scheduling/shutdown behavior and adds forum-thread-aware
messaging helpers. These are consumer-facing changes, including source and binary
compatibility breaks; use the migration guide before upgrading.

## Validation scope

The release entry point runs TelegramBotKit.Tests,
TelegramBotKit.Sample.PhotoSelector.Tests and the packed consumer fixture.
PhotoSelector uses published package references: its tests validate the sample's
application behavior, while Core tests and the packed consumer validate the local library.
Configuration startup checks do not replace manual Telegram interaction, public
webhook delivery checks or a separate Native AOT publish/run check.

Keep tokens in environment variables, user secrets or ignored `appsettings.Local.json`
files. Local files are not copied to publish output. Removing a token from the latest
configuration does not remove it from Git history: exposed tokens must be revoked
in BotFather, and affected published history must be cleaned before release.
