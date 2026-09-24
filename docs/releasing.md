# Releasing TelegramBotKit (NuGet)

The shared package version is **0.4.0-preview.1**, a SemVer prerelease.
Stable **0.4.0** is reserved for a later release. Every package inherits
`Directory.Build.props`; do not set versions in individual projects.

## Reproduce the validation locally

Install Python 3 and the exact .NET SDK **10.0.103** selected by `global.json`.
From the repository root, with an empty `artifacts/packages` directory:

```sh
python scripts/release.py check-version
python scripts/release.py build
```

The build command restores all four source projects, the full test project and
both samples in locked mode, builds Release, runs the complete test project,
builds both samples, packs all source projects, and validates the archives.
It stops on the first failure. It does not start bots or publish anything.
Existing dependency lock files must not be regenerated just to bypass a failure.

Expected output in `artifacts/packages`:

- Four `.nupkg` files: TelegramBotKit, TelegramBotKit.Hosting,
  TelegramBotKit.Routing and TelegramBotKit.Generators.
- Three `.snupkg` files for the runtime packages. The generator deliberately
  disables separate symbols and includes its portable PDB beside the analyzer.
- `SHA256SUMS` covering all seven archives.

Validation checks package identities and versions, MIT license, README, icon,
repository commit, runtime DLL/XML documentation, generator analyzer layout,
internal dependency versions, portable PDBs and embedded SourceLink URLs.
To repeat only artifact validation: `python scripts/release.py validate`.
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

## 0.4.0-preview.1 validation record

Validated against baseline `e18989fc868bbdefc719a4da6e46aa8522c36861`
plus the release-preparation working tree, on Windows x64 using SDK 10.0.103:

- All seven project restores passed in locked mode.
- All four package projects and both samples built in Release, with zero warnings.
- All 165 .NET tests passed, with zero skipped or failed tests.
- Four publication-gate regression tests passed; workflow YAML parsed successfully.
- Four packages and three symbol archives passed artifact validation.
- No production-code changes were necessary.
- nuget.org reported the preview version absent for all four package IDs at validation time.
  Publication rechecks availability; this observation does not reserve the version.

Only the ConsolePolling lock file required regeneration: its SDK-injected
ILCompiler/ILLink packages were 10.0.12, inconsistent with SDK 10.0.103, which
requires 10.0.3. Its stale `net10.0/win-x64` target was removed for the current
RID-independent build, and the regenerated project dependency reflects the
preview version. Application dependency versions and all other locks are unchanged.

The local Windows TLS provider prevented .NET from contacting nuget.org.
Validation used a local feed populated over HTTPS with the exact locked NuGet
archives, plus SDK-required 10.0.3 AOT tooling. Locked restores verified package
content hashes. This environment workaround does not change workflow feed settings.
Local build output and checksums are in `artifacts/packages`; the build log is
`artifacts/preview-build.log`. No packages, tags or GitHub releases were published.
