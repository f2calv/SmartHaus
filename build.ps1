[CmdletBinding()]
param(
    # Authenticate to ghcr.io (via gh CLI) and push the image instead of a local-only build.
    [switch]$Push,
    # Release uses Dockerfile; Debug uses Dockerfile.Debug and pulls in the local sibling repos.
    [ValidateSet("Debug", "Release")][string]$Configuration = "Debug",
    # Image tag override. Default: GitVersion FullSemVer when -Push, otherwise "latest-dev".
    [string]$Tag,
    # Target platform(s). Single-arch (e.g. linux/arm64) is much faster for the inner loop.
    [string]$Platforms = "linux/amd64,linux/arm64,linux/arm/v7",
    # Image repository name under $REGISTRY. "smarthaus" matches the CI/CD image.
    [string]$ImageName = "smarthaus",
    [string]$WorkloadName = "CasCap.App.Server"
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$REGISTRY = "ghcr.io/f2calv"
$REPO_ROOT = [IO.Path]::GetFullPath($PSScriptRoot)
$GIT_REPOSITORY = $REPO_ROOT | Split-Path -Leaf
$GIT_BRANCH = $(git -C $REPO_ROOT branch --show-current)
$GIT_COMMIT = $(git -C $REPO_ROOT rev-parse HEAD)

$GITHUB_WORKFLOW = "local"
$GITHUB_RUN_ID = 0
$GITHUB_RUN_NUMBER = 0

$BUILDER_NAME = "smarthaus1"

# Sibling repositories copied into deps/ for Debug (Dockerfile.Debug) builds, so a
# fix/feature can be verified without first publishing those repos.
$DEP_REPOS = @("CasCap.Common", "CasCap.Api.Azure")

function Resolve-Tag {
    if ($Tag) { return $Tag.ToLower() }
    if ($Push) {
        if (-not (Get-Command dotnet-gitversion -ErrorAction SilentlyContinue)) {
            Write-Host "dotnet-gitversion not found. Installing GitVersion.Tool globally..." -ForegroundColor Cyan
            dotnet tool install -g GitVersion.Tool
            if ($LASTEXITCODE -ne 0) { throw "Failed to install GitVersion.Tool. Run: dotnet tool install -g GitVersion.Tool" }
            $toolsPath = Join-Path $HOME ".dotnet/tools"
            if ($env:PATH -notlike "*$toolsPath*") { $env:PATH = "$toolsPath$([IO.Path]::PathSeparator)$env:PATH" }
        }
        return "$(dotnet-gitversion $REPO_ROOT /showvariable FullSemVer)".Trim().ToLower()
    }
    return "latest-dev"
}

function Connect-Ghcr {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh CLI not found. Install: https://cli.github.com"
    }
    if (-not (gh auth status 2>&1 | Select-String -SimpleMatch "write:packages")) {
        Write-Host "Refreshing gh auth to add the write:packages scope..." -ForegroundColor Cyan
        gh auth refresh -h github.com -s write:packages
    }
    $ghUser = "$(gh api user --jq .login)".Trim()
    Write-Host "Authenticating Docker to ghcr.io as $ghUser..." -ForegroundColor Cyan
    gh auth token | docker login ghcr.io -u $ghUser --password-stdin
    if ($LASTEXITCODE -ne 0) { throw "docker login ghcr.io failed." }
}

function Sync-Deps {
    $parent = Split-Path $REPO_ROOT -Parent
    foreach ($repo in $DEP_REPOS) {
        $src = Join-Path $parent $repo
        if (-not (Test-Path $src)) {
            throw "Debug build requires sibling repo '$repo' at '$src' (not found)."
        }
        $dst = Join-Path (Join-Path $REPO_ROOT "deps") $repo
        $resolvedSource = [IO.Path]::GetFullPath($src).TrimEnd([IO.Path]::DirectorySeparatorChar)
        $resolvedDestination = [IO.Path]::GetFullPath($dst).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ($resolvedDestination.StartsWith("$resolvedSource$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to mirror '$resolvedSource' into its own descendant '$resolvedDestination'."
        }
        Write-Host "Syncing $repo -> deps/$repo" -ForegroundColor Cyan
        & robocopy $src $dst /MIR `
            /XD bin obj .git .vs node_modules deps `
            /XF "appsettings.Local*.json" "*.user" `
            /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $repo (exit $LASTEXITCODE)." }
    }
    $global:LASTEXITCODE = 0
}

function Invoke-Build {
    $dockerfile = if ($Configuration -eq "Debug") { "Dockerfile.Debug" } else { "Dockerfile" }
    $tagValue = Resolve-Tag
    $img = "$REGISTRY/$($ImageName.ToLower()):$tagValue"

    if ($Configuration -eq "Debug") { Sync-Deps }
    if ($Push) { Connect-Ghcr }

    & docker buildx inspect $BUILDER_NAME 2>$null
    if ($LASTEXITCODE -ne 0) {
        & docker buildx create --name $BUILDER_NAME
    }
    & docker buildx use $BUILDER_NAME

    # Multi-arch manifests cannot be loaded into the local engine; --push publishes,
    # --pull just validates the build (the original local-only behaviour).
    $publishArg = if ($Push) { "--push" } else { "--pull" }

    & docker buildx build -t $img `
        -f (Join-Path $REPO_ROOT $dockerfile) `
        --build-arg WORKLOAD=$WorkloadName `
        --build-arg CONFIGURATION=$Configuration `
        --build-arg GIT_REPOSITORY=$GIT_REPOSITORY `
        --build-arg GIT_BRANCH=$GIT_BRANCH `
        --build-arg GIT_COMMIT=$GIT_COMMIT `
        --build-arg GIT_TAG=$tagValue `
        --build-arg GITHUB_WORKFLOW=$GITHUB_WORKFLOW `
        --build-arg GITHUB_RUN_ID=$GITHUB_RUN_ID `
        --build-arg GITHUB_RUN_NUMBER=$GITHUB_RUN_NUMBER `
        --platform $Platforms `
        $publishArg `
        $REPO_ROOT
    if ($LASTEXITCODE -ne 0) { throw "docker buildx build failed with exit code $LASTEXITCODE" }

    if ($Push) {
        Write-Host "Pushed: $img" -ForegroundColor Green
    }
    else {
        Write-Host "Built (not pushed): $img" -ForegroundColor Green
    }
}

Invoke-Build
