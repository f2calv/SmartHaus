#Requires -Version 7
<#
.SYNOPSIS
    Fast inner-loop deploy: build and push a Debug image, then roll it out through ArgoCD.
.DESCRIPTION
    Builds a Debug image with local sibling dependencies, optionally packages its
    umbrella chart, and patches a GitOps ApplicationSet manifest. Deployment-specific
    defaults can be stored in the gitignored deploy.local.psd1 file; explicit command
    line parameters override those defaults.

    The mutable latest-dev image tag requires a unique pod annotation to force a
    rollout. The script updates the shared annotation and every alias-local
    podAnnotations map so workloads with additional network annotations also roll.
.PARAMETER DeployConfigPath
    Local PowerShell data file containing default parameter values.
.PARAMETER ConfigMapPath
    Optional path, relative to ManifestRepo, of the ConfigMap whose
    data.appsettings.Local.json value is sourced from LocalAppSettingsPath.
.PARAMETER LocalAppSettingsPath
    Private appsettings file copied into the caller-supplied ConfigMap.
.EXAMPLE
    ./deploy.ps1
.EXAMPLE
    ./deploy.ps1 -SkipBuild -ManifestRepo <path-to-gitops-repo> -ManifestPath <manifest-path> -ConfigMapPath <configmap-path>
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
    [string]$DeployConfigPath = (Join-Path $PSScriptRoot "deploy.local.psd1"),
    [string]$ManifestRepo,
    [string]$ManifestPath,
    [string]$ConfigMapPath,
    [string]$LocalAppSettingsPath = (Join-Path $PSScriptRoot "appsettings.Local.json"),
    [string]$ImageRepository,
    [switch]$SkipBuild,
    [switch]$NoCommit,
    [switch]$SkipMigrationCheck,
    [switch]$Chart,
    [Alias("OnlyDashboards")][switch]$OnlyCharts,
    [string]$ChartPath,
    [string]$ChartRegistry = "ghcr.io",
    [string]$ChartRepository,
    [string]$DashboardChartPath,
    [string]$DashboardChartRepository,
    [string]$DashboardManifestPath,
    [string]$ChartVersion,
    [string]$DeploymentName,
    [string]$PodAnnotationName,
    [string]$MigrationProject,
    [string]$MigrationContext,
    [string]$MigrationConnectionStringEnvironmentVariable,
    [string]$MigrationConnectionString,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$REPO_ROOT = [IO.Path]::GetFullPath($PSScriptRoot)
if (Test-Path $DeployConfigPath) {
    $deployConfig = Import-PowerShellDataFile $DeployConfigPath
    foreach ($setting in $deployConfig.GetEnumerator()) {
        if (-not $PSBoundParameters.ContainsKey($setting.Key)) {
            Set-Variable -Name $setting.Key -Value $setting.Value
        }
    }
}
$repositoryName = Split-Path $REPO_ROOT -Leaf
if ([string]::IsNullOrWhiteSpace($DeploymentName)) { $DeploymentName = $repositoryName.ToLowerInvariant() }
if ([string]::IsNullOrWhiteSpace($ImageRepository)) { $ImageRepository = "ghcr.io/f2calv/$($repositoryName.ToLowerInvariant())" }
if (-not $OnlyCharts -and [string]::IsNullOrWhiteSpace($PodAnnotationName)) {
    throw "-PodAnnotationName is required. Supply it explicitly or in '$DeployConfigPath'."
}

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

if ([string]::IsNullOrWhiteSpace($ManifestRepo)) {
    throw "-ManifestRepo is required. Supply it explicitly or in '$DeployConfigPath'."
}
$manifestPathToPatch = if ($OnlyCharts) { $DashboardManifestPath } else { $ManifestPath }
if ([string]::IsNullOrWhiteSpace($manifestPathToPatch)) {
    $requiredParameter = if ($OnlyCharts) { "DashboardManifestPath" } else { "ManifestPath" }
    throw "-$requiredParameter is required for this deployment mode. Supply it explicitly or in '$DeployConfigPath'."
}
$manifest = Join-Path $ManifestRepo $manifestPathToPatch
$configMap = if ([string]::IsNullOrWhiteSpace($ConfigMapPath)) { $null } else { Join-Path $ManifestRepo $ConfigMapPath }

if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    throw "yq not found. Install it (e.g. winget install MikeFarah.yq) - required to patch the GitOps manifest."
}
if (-not (Test-Path $manifest)) {
    throw "GitOps manifest not found at '$manifest'."
}
if ($configMap -and -not (Test-Path $configMap)) {
    throw "GitOps ConfigMap not found at '$configMap'."
}
if ($configMap -and -not (Test-Path $LocalAppSettingsPath)) {
    throw "Local appsettings file not found at '$LocalAppSettingsPath'."
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

function Update-ManifestRepository {
    $workingTreeChanges = @(git -C $ManifestRepo status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw "git status failed for '$ManifestRepo'." }
    if ($workingTreeChanges.Count -gt 0) {
        throw "GitOps repository '$ManifestRepo' has local changes. Commit, stash, or discard them before deploying."
    }

    Write-Host "Refreshing GitOps repository '$ManifestRepo'..." -ForegroundColor Cyan
    git -C $ManifestRepo fetch --prune
    if ($LASTEXITCODE -ne 0) { throw "git fetch failed for '$ManifestRepo'." }
    $upstream = "$(git -C $ManifestRepo rev-parse --abbrev-ref --symbolic-full-name '@{upstream}')".Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($upstream)) {
        throw "The current GitOps branch has no upstream branch."
    }
    git -C $ManifestRepo merge --ff-only $upstream
    if ($LASTEXITCODE -ne 0) {
        throw "GitOps branch could not be fast-forwarded to '$upstream'. Resolve its branch state before deploying."
    }
}

function Assert-NoModelDrift {
    if ([string]::IsNullOrWhiteSpace($MigrationProject)) { return }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet not found - required for the EF model-drift check."
    }
    Write-Host "Checking EF model/migration drift..." -ForegroundColor Cyan
    $previousArtifactsPath = $env:ArtifactsPath
    $previousBaseOutputPath = $env:BaseOutputPath
    $previousConnectionString = if ($MigrationConnectionStringEnvironmentVariable) {
        [Environment]::GetEnvironmentVariable($MigrationConnectionStringEnvironmentVariable)
    }
    $driftOutputPath = Join-Path ([IO.Path]::GetTempPath()) "ef-output-$PID-$([Guid]::NewGuid().ToString('N'))"
    $env:ArtifactsPath = $null
    $env:BaseOutputPath = Join-Path $driftOutputPath "bin\"
    if ($MigrationConnectionStringEnvironmentVariable) {
        [Environment]::SetEnvironmentVariable($MigrationConnectionStringEnvironmentVariable, $MigrationConnectionString)
    }
    $dataProject = Join-Path $REPO_ROOT $MigrationProject
    Push-Location $REPO_ROOT
    try {
        dotnet tool restore | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed (needed for dotnet-ef)." }
        dotnet restore $dataProject | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for the EF model-drift check." }
        $efArguments = @("ef", "migrations", "has-pending-model-changes", "--project", $dataProject, "--startup-project", $dataProject)
        if ($MigrationContext) { $efArguments += @("--context", $MigrationContext) }
        & dotnet @efArguments
        if ($LASTEXITCODE -ne 0) {
            throw "EF model/migration drift check failed. Review the dotnet ef output above; add a migration only when it reports pending model changes."
        }
    }
    finally {
        Pop-Location
        $env:ArtifactsPath = $previousArtifactsPath
        $env:BaseOutputPath = $previousBaseOutputPath
        if ($MigrationConnectionStringEnvironmentVariable) {
            [Environment]::SetEnvironmentVariable($MigrationConnectionStringEnvironmentVariable, $previousConnectionString)
        }
        Remove-Item $driftOutputPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Update-ManifestRepository

$ts = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')

if (-not $SkipMigrationCheck -and -not $OnlyCharts) { Assert-NoModelDrift }

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
    if ([string]::IsNullOrWhiteSpace($ChartPath) -or [string]::IsNullOrWhiteSpace($ChartRepository)) {
        throw "ChartPath and ChartRepository are required for chart deployment. Supply them explicitly or in '$DeployConfigPath'."
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

    $pkgDir = Join-Path ([IO.Path]::GetTempPath()) "$DeploymentName-chart-$ts"
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
    $env:DEPLOY_DASHBOARD_CHART_VERSION = $ChartVersion
    Write-Host "Patching $manifest (targetRevision=$ChartVersion)" -ForegroundColor Cyan
    yq -i '.spec.template.spec.source.targetRevision = strenv(DEPLOY_DASHBOARD_CHART_VERSION)' $manifest
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
    git -C $ManifestRepo commit -m "deploy($DeploymentName-dashboards): chart=${ChartVersion}"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
    git -C $ManifestRepo push
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }
    Write-Host "Deployed dashboard chart $ChartVersion; ArgoCD will sync the dashboard ApplicationSet." -ForegroundColor Green
    return
}

$gitOpsPaths = [System.Collections.Generic.List[string]]::new()
$gitOpsPaths.Add($manifestPathToPatch)
if ($configMap) {
    Write-Host "Syncing $LocalAppSettingsPath -> $configMap" -ForegroundColor Cyan
    $env:DEPLOY_LOCAL_APPSETTINGS = [IO.File]::ReadAllText($LocalAppSettingsPath)
    yq -i '.data."appsettings.Local.json" = strenv(DEPLOY_LOCAL_APPSETTINGS) | .data."appsettings.Local.json" style="literal"' $configMap
    if ($LASTEXITCODE -ne 0) { throw "yq failed to update the appsettings ConfigMap." }
    $gitOpsPaths.Add($ConfigMapPath)
}

$stamp = "$(Get-GitVersion)+$ts"
$patchMsg = "repository=$ImageRepository, tag=$Tag, pullPolicy=Always, deployed-version=$stamp"
if ($Chart) { $patchMsg += ", targetRevision=$ChartVersion" }
Write-Host "Patching $manifest ($patchMsg)" -ForegroundColor Cyan

$env:DEPLOY_IMG_REPO = $ImageRepository
$env:DEPLOY_IMG_TAG = $Tag
$env:DEPLOY_STAMP = $stamp
$assignments = [System.Collections.Generic.List[string]]::new()
if ($Chart) {
    $env:DEPLOY_CHART_VERSION = $ChartVersion
    $assignments.Add('.spec.template.spec.source.targetRevision = strenv(DEPLOY_CHART_VERSION)')
}
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.repository = strenv(DEPLOY_IMG_REPO)')
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.tag = strenv(DEPLOY_IMG_TAG)')
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.image.pullPolicy = "Always"')
$env:DEPLOY_POD_ANNOTATION = $PodAnnotationName
$assignments.Add('.spec.template.spec.source.helm.valuesObject._shared.podAnnotations[strenv(DEPLOY_POD_ANNOTATION)] = strenv(DEPLOY_STAMP)')
$assignments.Add('(.spec.template.spec.source.helm.valuesObject[] | select(has("podAnnotations")).podAnnotations[strenv(DEPLOY_POD_ANNOTATION)]) = strenv(DEPLOY_STAMP)')
$yqExpr = $assignments -join ' | '
yq -i $yqExpr $manifest
if ($LASTEXITCODE -ne 0) { throw "yq failed to patch the manifest." }

git -C $ManifestRepo diff --quiet -- $gitOpsPaths
if ($LASTEXITCODE -eq 0) {
    Write-Host "No GitOps changes detected - nothing to commit." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo --no-pager diff --stat -- $gitOpsPaths
if ($NoCommit) {
    Write-Host "GitOps files patched but not committed (-NoCommit). Review the diff, then commit/push manually." -ForegroundColor Yellow
    return
}
git -C $ManifestRepo add -- $gitOpsPaths
$commitMsg = if ($Chart) { "deploy($DeploymentName): ${Tag} ${stamp} chart=${ChartVersion}" } else { "deploy($DeploymentName): ${Tag} ${stamp}" }
git -C $ManifestRepo commit -m $commitMsg
if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
git -C $ManifestRepo push
if ($LASTEXITCODE -ne 0) { throw "git push failed." }
Write-Host "Deployed: ${ImageRepository}:${Tag}; ArgoCD will sync the application." -ForegroundColor Green
