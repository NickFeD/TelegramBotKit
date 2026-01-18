# Releasing TelegramBotKit (NuGet)

This repo uses GitHub Actions to build, pack, and publish all NuGet packages.

## CI (every push / PR)
Workflow: `.github/workflows/ci.yml`

- Restores and builds in `Release`
- Packs **all** projects under `src/*/*.csproj`
- Uploads `.nupkg` and `.snupkg` as workflow artifacts

## Publishing a release
Workflow: `.github/workflows/release.yml`

Triggers:
- Push a tag like `v0.3.0`
- Or run `workflow_dispatch` manually (useful for dry runs)

### 1) Bump the version
Update `Version` in `Directory.Build.props`, commit, push.

### 2) Create and push the tag

```bash
git tag v0.3.0
git push origin v0.3.0
```

That tag will:
- build + pack all projects under `src/`
- create a GitHub Release with attached artifacts
- publish all `.nupkg` and `.snupkg` to nuget.org

## NuGet authentication options

### Option A: Classic API key
Create a nuget.org API key and set a GitHub Actions secret:
- `NUGET_API_KEY`

### Option B: Trusted Publishing (recommended)
Trusted Publishing uses GitHub OIDC to obtain a short-lived nuget.org API key at publish time (no long-lived secrets).

Setup:
1. On nuget.org, create a **Trusted Publishing** policy for this repository.
   - Repository owner: your GitHub user/org
   - Repository: `TelegramBotKit`
   - Workflow file: `release.yml`
2. Add a GitHub Actions secret:
   - `NUGET_USER` = your nuget.org *profile name* (not email)

Notes:
- If `NUGET_API_KEY` is present, the workflow uses it.
- Otherwise it falls back to Trusted Publishing (requires `NUGET_USER`).
