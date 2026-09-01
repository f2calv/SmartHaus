#Requires -Version 7
<#
.SYNOPSIS
    Fast inner-loop deploy: build and push a Debug image, then roll it out through ArgoCD.
.DESCRIPTION
    Builds a Debug image with local sibling dependencies, optionally packages the
    smarthaus umbrella chart, and patches a caller-supplied GitOps ApplicationSet
    manifest. The
    manifest repository and path are mandatory so deployment topology remains
    outside this public application repository.

    The mutable latest-dev image tag requires a unique pod annotation to force a
    rollout. The script updates the shared annotation and every alias-local
    podAnnotations map so workloads with additional network annotations also roll.
.EXAMPLE
    ./deploy.ps1 -ManifestRepo <path-to-gitops-repo> -ManifestPath <manifest-path>
.EXAMPLE
    ./deploy.ps1 -ManifestRepo <path-to-gitops-repo> -ManifestPath <manifest-path> -Chart
.EXAMPLE
    ./deploy.ps1 -ManifestRepo <path-to-gitops-repo> -ManifestPath <manifest-path> -SkipBuild -Chart
#>
[CmdletBinding()]
param(
    [string]$Tag = "latest-dev",
    [string]$Platforms = "linux/arm64",
    [Parameter(Mandatory)][string]$ManifestRepo,
    [Parameter(Mandatory)][string]$ManifestPath,
    [string]$ImageRepository = "ghcr.io/f2calv/smarthaus",
    [switch]$SkipBuild,
    [switch]$NoCommit,
    [switch]$Chart,
    [string]$ChartPath = "charts/smarthaus",
    [string]$ChartRegistry = "ghcr.io",
    [string]$ChartRepository = "f2calv/charts/smarthaus",
    [string]$ChartVersion,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$Rest = @($Rest | Where-Object { $null -ne $_ })
if ($Rest.Count -gt 0) {
    $filtered = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $Rest.Count; $i++) {
        $arg = $Rest[$i]
        if ($arg -match '^-Configuration(?::(.+))?$') {
            $val = if ($Matches[1]) { $Matches[1] } elseif ($i + 1 -lt $Rest.Count) { $Rest[++$i] } else { 'Debug' }
            if ($val -ne 'Debug') {
                throw "deploy.ps1 only performs Debug builds; '-Configuration $val' is not supported. Use build.ps1 directly for $val."
            }
            Write-Host "Ignoring redundant '-Configuration Debug' (deploy.ps1 always builds Debug)." -ForegroundColor Yellow
            continue
        }
        $filtered.Add($arg)
    }
    $Rest = $filtered.ToArray()
}

$REPO_ROOT = [IO.Path]::GetFullPath($PSScriptRoot)
$manifest = Join-Path $ManifestRepo $ManifestPath

if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    throw "yq not found. Install it (e.g. winget install MikeFarah.yq) - required to patch the GitOps manifest."
}
if (-not (Test-Path $manifest)) {
    throw "GitOps manifest not found at '$manifest'."
}

function Get-GitVersion {
    if (-not (Get-Command dotnet-gitversion -ErrorAction SilentlyContinue)) {
        Write-Host "dotnet-gitversion not found. Installing GitVersion.Tool globally..." -ForegroundColor Cyan
        dotnet tool install -g GitVersion.Tool
        if ($LASTEXITCODE -ne 0) { throw "Failed to install GitVersion.Tool. Run: dotnet tool install -g GitVersion.Tool" }
        $toolsPath = Join-Path $HOME ".dotnet/tools"
        if ($env:PATH -notlike "*$toolsPath*") { $env:PATH = "$toolsPath$([IO.Path]::PathSeparator)$env:PATH" }
    }
    return "$(dotnet-gitversion $REPO_ROOT /showvariable FullSemVer)".Trim()
}

function Connect-HelmRegistry {
    param([Parameter(Mandatory)][string]$Registry)
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh CLI not found (needed to authenticate Helm to $Registry). Install: https://cli.github.com"
    }
    $ghUser = "$(gh api user --jq .login)".Trim()
    Write-Host "Authenticating Helm to $Registry as $ghUser..." -ForegroundColor Cyan
    gh auth token | helm registry login $Registry --username $ghUser --password-stdin
    if ($LASTEXITCODE -ne 0) { throw "helm registry login $Registry failed." }
}

$ts = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')

if (-not $SkipBuild) {
    & "$PSScriptRoot/build.ps1" -Push -Configuration Debug -Platforms $Platforms -Tag $Tag @Rest
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed with exit code $LASTEXITCODE" }
}
else {
    Write-Host "Skipping build/push (-SkipBuild); re-rolling existing ${ImageRepository}:${Tag}" -ForegroundColor Yellow
}

if ($Chart) {
    if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
        throw "helm not found. Install Helm to use -Chart."
    }
    if (-not $ChartVersion) { $ChartVersion = "0.0.0-dev.$ts" }
    $chartDir = Join-Path $REPO_ROOT $ChartPath
    if (-not (Test-Path (Join-Path $chartDir 'Chart.yaml'))) {
        throw "Chart not found at '$chartDir' (expected Chart.yaml). Pass -ChartPath to override."
    }
    $chartName = Split-Path $ChartPath -Leaf
    $ociTarget = "oci://$ChartRegistry/$($ChartRepository -replace '/[^/]+$', '')"

    Connect-HelmRegistry -Registry $ChartRegistry

    $pkgDir = Join-Path ([IO.Path]::GetTempPath()) "smarthaus-chart-$ts"
    New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
    try {
        Write-Host "Packaging $chartName $ChartVersion (appVersion=$Tag) from $ChartPath" -ForegroundColor Cyan
        helm dependency update $chartDir
        if ($LASTEXITCODE -ne 0) { throw "helm dependency update failed." }
        helm package $chartDir --version $ChartVersion --app-version $Tag --destination $pkgDir
        if ($LASTEXITCODE -ne 0) { throw "helm package failed." }
        $tgz = Join-Path $pkgDir "$chartName-$ChartVersion.tgz"
        Write-Host "Pushing $tgz -> $ociTarget" -ForegroundColor Cyan
        helm push $tgz $ociTarget
        if ($LASTEXITCODE -ne 0) { throw "helm push failed." }
    }
    finally {
        Remove-Item $pkgDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Pushed chart: ${ChartRegistry}/${ChartRepository}:${ChartVersion}" -ForegroundColor Green
}

$stamp = "$(Get-GitVersion)+$ts"
$patchMsg = "repository=$ImageRepository, tag=$Tag, pullPolicy=Always, deployed-version=$stamp"
if ($Chart) { $patchMsg += ", targetRevision=$ChartVersion" }
Write-Host "Patching $manifest ($patchMsg)" -ForegroundColor Cyan

$env:SMARTHAUS_IMG_REPO = $ImageRepository
$env:SMARTHAUS_IMG_TAG = $Tag
$env:SMARTHAUS_DEPLOY_STAMP = $stamp
$assignments = [System.Collections.Generic.List[string]]::new()
if ($Chart) {
    $env:SMARTHAUS_CHART_VERSION = $ChartVersion
    $assignments.Add('.spec.template.spec.source.targetRevision = strenv(SMARTHAUS_CHART_VERSION)')
}
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.repository = strenv(SMARTHAUS_IMG_REPO)')
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.tag = strenv(SMARTHAUS_IMG_TAG)')
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.pullPolicy = "Always"')
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.podAnnotations["smarthaus.f2calv.io/deployed-version"] = strenv(SMARTHAUS_DEPLOY_STAMP)')
$assignments.Add('(.spec.template.spec.source.helm.valuesObject[] | select(has("podAnnotations")).podAnnotations["smarthaus.f2calv.io/deployed-version"]) = strenv(SMARTHAUS_DEPLOY_STAMP)')
$yqExpr = $assignments -join ' | '
yq -i $yqExpr $manifest
if ($LASTEXITCODE -ne 0) { throw "yq failed to patch the manifest." }

git -C $ManifestRepo diff --quiet -- $ManifestPath
if ($LASTEXITCODE -eq 0) {
    Write-Host "No manifest changes detected - nothing to commit." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo --no-pager diff --stat -- $ManifestPath
if ($NoCommit) {
    Write-Host "Manifest patched but not committed (-NoCommit). Review the diff, then commit/push manually." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo add -- $ManifestPath
$commitMsg = if ($Chart) { "deploy(smarthaus): ${Tag} ${stamp} chart=${ChartVersion}" } else { "deploy(smarthaus): ${Tag} ${stamp}" }
git -C $ManifestRepo commit -m $commitMsg
if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
git -C $ManifestRepo push
if ($LASTEXITCODE -ne 0) { throw "git push failed." }
Write-Host "Deployed: ${ImageRepository}:${Tag}; ArgoCD will sync the application." -ForegroundColor Green