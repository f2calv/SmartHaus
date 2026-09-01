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
.EXAMPLE
    ./deploy.ps1 -OnlyCharts -ManifestRepo <path-to-gitops-repo> -DashboardManifestPath <dashboard-manifest-path>
#>
[CmdletBinding()]
param(
    [string]$Tag = "latest-dev",
    [string]$Platforms = "linux/arm64",
    [Parameter(Mandatory)][string]$ManifestRepo,
    [string]$ManifestPath,
    [string]$ImageRepository = "ghcr.io/f2calv/smarthaus",
    [switch]$SkipBuild,
    [switch]$NoCommit,
    [switch]$Chart,
    [Alias("OnlyDashboards")][switch]$OnlyCharts,
    [string]$ChartPath = "charts/smarthaus",
    [string]$ChartRegistry = "ghcr.io",
    [string]$ChartRepository = "f2calv/charts/smarthaus",
    [string]$DashboardChartPath = "charts/dashboards",
    [string]$DashboardChartRepository = "f2calv/charts/dashboards",
    [string]$DashboardManifestPath,
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
$manifestPathToPatch = if ($OnlyCharts) { $DashboardManifestPath } else { $ManifestPath }
if ([string]::IsNullOrWhiteSpace($manifestPathToPatch)) {
    $requiredParameter = if ($OnlyCharts) { "DashboardManifestPath" } else { "ManifestPath" }
    throw "-$requiredParameter is required for this deployment mode."
}
$manifest = Join-Path $ManifestRepo $manifestPathToPatch

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

if (-not $SkipBuild -and -not $OnlyCharts) {
    & "$PSScriptRoot/build.ps1" -Push -Configuration Debug -Platforms $Platforms -Tag $Tag @Rest
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed with exit code $LASTEXITCODE" }
}
else {
    if ($OnlyCharts) {
        Write-Host "Skipping image build/push (-OnlyCharts)." -ForegroundColor Yellow
    }
    else {
        Write-Host "Skipping build/push (-SkipBuild); re-rolling existing ${ImageRepository}:${Tag}" -ForegroundColor Yellow
    }
}

if ($Chart -or $OnlyCharts) {
    if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
        throw "helm not found. Install Helm to use -Chart."
    }
    if ($OnlyCharts) {
        $ChartPath = $DashboardChartPath
        $ChartRepository = $DashboardChartRepository
    }
    if (-not $ChartVersion) { $ChartVersion = "0.0.0-dev.$ts" }
    $chartDir = Join-Path $REPO_ROOT $ChartPath
    if (-not (Test-Path (Join-Path $chartDir 'Chart.yaml'))) {
        throw "Chart not found at '$chartDir' (expected Chart.yaml). Pass -ChartPath to override."
    }
    $chartName = Split-Path $ChartPath -Leaf
    $ociTarget = "oci://$ChartRegistry/$($ChartRepository -replace '/[^/]+$', '')"

    if ($OnlyCharts) {
        Get-ChildItem (Join-Path $chartDir "dashboards/*.json") | ForEach-Object {
            try { $null = Get-Content $_.FullName -Raw | ConvertFrom-Json }
            catch { throw "Invalid dashboard JSON '$($_.FullName)': $($_.Exception.Message)" }
        }
        helm lint $chartDir
        if ($LASTEXITCODE -ne 0) { throw "helm lint failed for '$chartDir'." }
        $null = helm template $chartName $chartDir
        if ($LASTEXITCODE -ne 0) { throw "helm template failed for '$chartDir'." }
    }

    Connect-HelmRegistry -Registry $ChartRegistry

    $pkgDir = Join-Path ([IO.Path]::GetTempPath()) "smarthaus-chart-$ts"
    New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
    try {
        Write-Host "Packaging $chartName $ChartVersion (appVersion=$Tag) from $ChartPath" -ForegroundColor Cyan
        helm dependency update $chartDir
        if ($LASTEXITCODE -ne 0) { throw "helm dependency update failed." }
        $appVersion = if ($OnlyCharts) { $ChartVersion } else { $Tag }
        helm package $chartDir --version $ChartVersion --app-version $appVersion --destination $pkgDir
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

if ($OnlyCharts) {
    $env:SMARTHAUS_DASHBOARD_CHART_VERSION = $ChartVersion
    Write-Host "Patching $manifest (targetRevision=$ChartVersion)" -ForegroundColor Cyan
    yq -i '.spec.template.spec.source.targetRevision = strenv(SMARTHAUS_DASHBOARD_CHART_VERSION)' $manifest
    if ($LASTEXITCODE -ne 0) { throw "yq failed to patch the dashboard manifest." }

    git -C $ManifestRepo diff --quiet -- $manifestPathToPatch
    if ($LASTEXITCODE -eq 0) {
        Write-Host "No dashboard manifest changes detected - nothing to commit." -ForegroundColor Yellow
        return
    }
    git -C $ManifestRepo --no-pager diff --stat -- $manifestPathToPatch
    if ($NoCommit) {
        Write-Host "Dashboard manifest patched but not committed (-NoCommit)." -ForegroundColor Yellow
        return
    }
    git -C $ManifestRepo add -- $manifestPathToPatch
    git -C $ManifestRepo commit -m "deploy(smarthaus-dashboards): chart=${ChartVersion}"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
    git -C $ManifestRepo push
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }
    Write-Host "Deployed dashboard chart $ChartVersion; ArgoCD will sync the dashboard ApplicationSet." -ForegroundColor Green
    return
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

git -C $ManifestRepo diff --quiet -- $manifestPathToPatch
if ($LASTEXITCODE -eq 0) {
    Write-Host "No manifest changes detected - nothing to commit." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo --no-pager diff --stat -- $manifestPathToPatch
if ($NoCommit) {
    Write-Host "Manifest patched but not committed (-NoCommit). Review the diff, then commit/push manually." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo add -- $manifestPathToPatch
$commitMsg = if ($Chart) { "deploy(smarthaus): ${Tag} ${stamp} chart=${ChartVersion}" } else { "deploy(smarthaus): ${Tag} ${stamp}" }
git -C $ManifestRepo commit -m $commitMsg
if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
git -C $ManifestRepo push
if ($LASTEXITCODE -ne 0) { throw "git push failed." }
Write-Host "Deployed: ${ImageRepository}:${Tag}; ArgoCD will sync the application." -ForegroundColor Green