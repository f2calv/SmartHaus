#!/usr/bin/env pwsh
#Requires -Version 7.4
<#
.SYNOPSIS
    Builds the application container image for local validation or publication.
.DESCRIPTION
    Selects Dockerfile.Debug for Debug builds and Dockerfile for Release builds.
    Debug builds discover sibling repositories from Dockerfile.Debug and mirror them
    into deps before invoking Docker Buildx. Push builds authenticate to GHCR and use
    GitVersion FullSemVer unless an explicit tag is supplied.
.PARAMETER Push
    Authenticates to GHCR and publishes the image instead of performing validation only.
.PARAMETER Configuration
    Build configuration. Debug uses Dockerfile.Debug; Release uses Dockerfile.
.PARAMETER Tag
    Image tag override. Defaults to GitVersion FullSemVer for push builds and latest-dev otherwise.
.PARAMETER Platforms
    Comma-separated Docker target platforms.
.PARAMETER ImageName
    Image repository name beneath ghcr.io/f2calv.
.PARAMETER WorkloadName
    Workload project name passed to the Docker build.
.EXAMPLE
    ./build.ps1 -Configuration Debug -Platforms linux/arm64
.EXAMPLE
    ./build.ps1 -Push -Configuration Release -Tag 1.2.3
.EXAMPLE
    ./build.ps1 -Configuration Debug -WhatIf
.NOTES
    Push builds require gh, Docker Buildx, and package-write access to GHCR.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    # Authenticate to ghcr.io (via gh CLI) and push the image instead of a local-only build.
    [switch]$Push,
    # Release uses Dockerfile; Debug uses Dockerfile.Debug and pulls in the local sibling repos.
    [ValidateSet("Debug", "Release")][string]$Configuration = "Debug",
    # Image tag override. Default: GitVersion FullSemVer when -Push, otherwise "latest-dev".
    [string]$Tag,
    # Target platform(s). Single-arch (e.g. linux/arm64) is much faster for the inner loop.
    [string]$Platforms = "linux/amd64,linux/arm64,linux/arm/v7",
    # Image repository name under $REGISTRY. Defaults to the repository directory name.
    [string]$ImageName = ([IO.Path]::GetFileName($PSScriptRoot).ToLowerInvariant()),
    [string]$WorkloadName = "CasCap.App.Server"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 3.0

#region Functions
function Get-DependencyRepositories {
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][string]$DockerfilePath)

    $dependencyRepositories = @([regex]::Matches(
        [IO.File]::ReadAllText($DockerfilePath),
        '(?m)^\s*COPY\s+deps/([^/\s]+)\s+/'
    ) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    if ($dependencyRepositories.Count -eq 0) {
        throw "No sibling dependencies were found in '$DockerfilePath'."
    }
    return $dependencyRepositories
}

function Get-BuildDockerfile {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][ValidateSet("Debug", "Release")][string]$Configuration)

    if ($Configuration -eq "Debug") { return "Dockerfile.Debug" }
    return "Dockerfile"
}

function Resolve-Tag {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$ExplicitTag,
        [switch]$Push,
        [Parameter(Mandatory)][string]$RepositoryRoot
    )

    if ($ExplicitTag) { return $ExplicitTag.ToLowerInvariant() }
    if ($Push) {
        if (-not (Get-Command dotnet-gitversion -ErrorAction SilentlyContinue)) {
            Write-Host "dotnet-gitversion not found. Installing GitVersion.Tool globally..." -ForegroundColor Cyan
            dotnet tool install -g GitVersion.Tool
            if ($LASTEXITCODE -ne 0) { throw "Failed to install GitVersion.Tool. Run: dotnet tool install -g GitVersion.Tool" }
            # Ensure the global tools path is on PATH for the current session.
            $toolsPath = Join-Path $HOME ".dotnet/tools"
            if ($env:PATH -notlike "*$toolsPath*") { $env:PATH = "$toolsPath$([IO.Path]::PathSeparator)$env:PATH" }
        }
        return "$(dotnet-gitversion $RepositoryRoot /showvariable FullSemVer)".Trim().ToLowerInvariant()
    }
    return "latest-dev"
}

function Connect-Ghcr {
    [CmdletBinding()]
    param()

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
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string[]]$DependencyRepositories
    )

    $parent = Split-Path $RepositoryRoot -Parent
    foreach ($repo in $DependencyRepositories) {
        $src = Join-Path $parent $repo
        if (-not (Test-Path $src)) {
            throw "Debug build requires sibling repo '$repo' at '$src' (not found)."
        }
        $dst = Join-Path (Join-Path $RepositoryRoot "deps") $repo
        $resolvedSource = [IO.Path]::GetFullPath($src).TrimEnd([IO.Path]::DirectorySeparatorChar)
        $resolvedDestination = [IO.Path]::GetFullPath($dst).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ($resolvedDestination.StartsWith("$resolvedSource$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to mirror '$resolvedSource' into its own descendant '$resolvedDestination'."
        }
        Write-Host "Syncing $repo -> deps/$repo" -ForegroundColor Cyan
        # /MIR mirrors (incremental). Exclude build output, VCS, and local-only secrets.
        & robocopy $src $dst /MIR `
            /XD bin obj .git .vs node_modules deps `
            /XF "appsettings.Local*.json" "*.user" `
            /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $repo (exit $LASTEXITCODE)." }
    }
    $global:LASTEXITCODE = 0
}

function Invoke-Build {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    $registry = "ghcr.io/f2calv"
    $repositoryRoot = [IO.Path]::GetFullPath($PSScriptRoot)
    $dockerfile = Get-BuildDockerfile -Configuration $Configuration
    $dependencyRepositories = Get-DependencyRepositories -DockerfilePath (Join-Path $repositoryRoot "Dockerfile.Debug")
    $image = "$registry/$($ImageName.ToLowerInvariant())"

    if (-not $PSCmdlet.ShouldProcess($image, "Build $Configuration container image")) { return }

    $gitRepository = Split-Path $repositoryRoot -Leaf
    $gitBranch = "$(git -C $repositoryRoot branch --show-current)".Trim()
    if ($LASTEXITCODE -ne 0) { throw "git branch failed for '$repositoryRoot'." }
    $gitCommit = "$(git -C $repositoryRoot rev-parse HEAD)".Trim()
    if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed for '$repositoryRoot'." }
    $tagValue = Resolve-Tag -ExplicitTag $Tag -Push:$Push -RepositoryRoot $repositoryRoot
    $imageReference = "${image}:$tagValue"
    $builderName = "${ImageName}1"

    if ($Configuration -eq "Debug") {
        Sync-Deps -RepositoryRoot $repositoryRoot -DependencyRepositories $dependencyRepositories
    }
    if ($Push) { Connect-Ghcr }

    & docker buildx inspect $builderName 2>$null
    if ($LASTEXITCODE -ne 0) {
        & docker buildx create --name $builderName
        if ($LASTEXITCODE -ne 0) { throw "docker buildx create failed with exit code $LASTEXITCODE" }
    }
    & docker buildx use $builderName
    if ($LASTEXITCODE -ne 0) { throw "docker buildx use failed with exit code $LASTEXITCODE" }

    # Multi-arch manifests cannot be loaded into the local engine; --push publishes,
    # --pull just validates the build (the original local-only behaviour).
    $publishArg = if ($Push) { "--push" } else { "--pull" }

    & docker buildx build -t $imageReference `
        -f (Join-Path $repositoryRoot $dockerfile) `
        --build-arg WORKLOAD=$WorkloadName `
        --build-arg CONFIGURATION=$Configuration `
        --build-arg GIT_REPOSITORY=$gitRepository `
        --build-arg GIT_BRANCH=$gitBranch `
        --build-arg GIT_COMMIT=$gitCommit `
        --build-arg GIT_TAG=$tagValue `
        --build-arg GITHUB_WORKFLOW=local `
        --build-arg GITHUB_RUN_ID=0 `
        --build-arg GITHUB_RUN_NUMBER=0 `
        --platform $Platforms `
        $publishArg `
        $repositoryRoot
    if ($LASTEXITCODE -ne 0) { throw "docker buildx build failed with exit code $LASTEXITCODE" }

    if ($Push) {
        Write-Host "Pushed: $imageReference" -ForegroundColor Green
    }
    else {
        Write-Host "Built (not pushed): $imageReference" -ForegroundColor Green
    }
}
#endregion Functions

#region Main Execution
if ($MyInvocation.InvocationName -ne '.') {
    try {
        Invoke-Build -WhatIf:$WhatIfPreference
        exit 0
    }
    catch {
        Write-Error -ErrorAction Continue "build.ps1 failed: $($_.Exception.Message)"
        exit 1
    }
}
#endregion Main Execution
