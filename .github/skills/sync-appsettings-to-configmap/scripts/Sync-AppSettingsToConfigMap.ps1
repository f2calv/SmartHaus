#!/usr/bin/env pwsh
#Requires -Version 7.4

<#
.SYNOPSIS
    Synchronizes an appsettings file into a tracked GitOps ConfigMap.
.DESCRIPTION
    Loads private GitOps coordinates from gitignored deployment data, validates
    an appsettings JSON file, and replaces one configured ConfigMap data value.
    Source content is never written to
    output, command-line arguments, or temporary files.
.PARAMETER SourcePath
    Appsettings source path contained by the application repository.
.PARAMETER ApplicationRoot
    Application repository root. Defaults to the repository containing this skill.
.PARAMETER DeployConfigPath
    Gitignored PowerShell data containing ManifestRepo and AppSettingsConfigMapPath.
.PARAMETER ManifestRepo
    Optional GitOps checkout path overriding the local deployment data.
.PARAMETER AppSettingsConfigMapPath
    Optional GitOps-relative ConfigMap path overriding the local deployment data.
.PARAMETER AppSettingsConfigMapName
    Optional ConfigMap metadata name overriding the local deployment data.
.PARAMETER AppSettingsConfigMapKey
    Optional ConfigMap data key. Defaults to appsettings.Local.json.
.EXAMPLE
    ./Sync-AppSettingsToConfigMap.ps1 -WhatIf
.EXAMPLE
    ./Sync-AppSettingsToConfigMap.ps1
.NOTES
    Requires PowerShell 7.4, Git, and Mike Farah yq.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$SourcePath,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ApplicationRoot,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$DeployConfigPath,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ManifestRepo,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$AppSettingsConfigMapPath,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$AppSettingsConfigMapName,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$AppSettingsConfigMapKey = 'appsettings.Local.json'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 3.0

function ConvertTo-CanonicalJson {
    <#
    .SYNOPSIS
        Parses JSON with appsettings-compatible comments and trailing commas.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $DocumentOptions = [Text.Json.JsonDocumentOptions]::new()
    $DocumentOptions.AllowTrailingCommas = $true
    $DocumentOptions.CommentHandling = [Text.Json.JsonCommentHandling]::Skip
    $Document = [Text.Json.JsonDocument]::Parse($Content, $DocumentOptions)
    try {
        return [Text.Json.JsonSerializer]::Serialize(
            [object]$Document.RootElement,
            [Text.Json.JsonElement],
            [Text.Json.JsonSerializerOptions]$null)
    }
    finally {
        $Document.Dispose()
    }
}

function Invoke-YqScalar {
    <#
    .SYNOPSIS
        Reads one scalar from a YAML document and fails on yq errors.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Expression,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Value = & yq -r $Expression $Path
    if ($LASTEXITCODE -ne 0) {
        throw 'yq failed while validating the target ConfigMap.'
    }

    return "$Value".Trim()
}

function Resolve-ContainedPath {
    <#
    .SYNOPSIS
        Resolves a path and rejects escapes through relative paths or reparse points.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $AbsoluteRoot = [IO.Path]::GetFullPath($RootPath)
    $RootItem = Get-Item -LiteralPath $AbsoluteRoot -Force
    $RootIsReparsePoint = ($RootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    $RootHasLinkType = $RootItem.PSObject.Properties.Name -contains 'LinkType' -and
    $null -ne $RootItem.LinkType
    if ($RootIsReparsePoint -or $RootHasLinkType) {
        throw "The $Description repository root cannot be a symbolic link or reparse point."
    }

    $ResolvedRoot = (Resolve-Path -LiteralPath $AbsoluteRoot).Path
    $CandidatePath = if ([IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $ResolvedRoot $Path
    }
    $AbsolutePath = [IO.Path]::GetFullPath($CandidatePath)
    $Comparison = if ([OperatingSystem]::IsWindows()) {
        [StringComparison]::OrdinalIgnoreCase
    }
    else {
        [StringComparison]::Ordinal
    }
    $RootPrefix = $ResolvedRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $AbsolutePath.StartsWith($RootPrefix, $Comparison)) {
        throw "The $Description must be inside its configured repository."
    }

    $CurrentPath = $ResolvedRoot
    $RelativePath = [IO.Path]::GetRelativePath($ResolvedRoot, $AbsolutePath)
    foreach ($Segment in $RelativePath.Split(
            @([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries)) {
        $CurrentPath = Join-Path $CurrentPath $Segment
        $Item = Get-Item -LiteralPath $CurrentPath -Force
        $IsReparsePoint = ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        $HasLinkType = $Item.PSObject.Properties.Name -contains 'LinkType' -and
        $null -ne $Item.LinkType
        if ($IsReparsePoint -or $HasLinkType) {
            throw "The $Description cannot traverse a symbolic link or reparse point."
        }
    }

    return (Resolve-Path -LiteralPath $AbsolutePath).Path
}

function Sync-AppSettingsToConfigMap {
    <#
    .SYNOPSIS
        Validates and synchronizes an appsettings ConfigMap data value.
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
        [string]$ConfigMapName,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9._-]+$')]
        [string]$ConfigMapKey
    )

    $Yq = Get-Command yq -ErrorAction SilentlyContinue
    if ($null -eq $Yq) {
        throw 'Mike Farah yq is required but was not found on PATH.'
    }
    $YqVersion = & $Yq.Source --version
    if ($LASTEXITCODE -ne 0 -or "$YqVersion" -notmatch 'github\.com/mikefarah/yq') {
        throw 'The yq command on PATH is not the supported Mike Farah implementation.'
    }

    $ResolvedSourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
    $ResolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $ResolvedSourcePath = Resolve-ContainedPath `
        -RootPath $ResolvedSourceRoot `
        -Path $SourcePath `
        -Description 'source appsettings file'
    $ResolvedTargetPath = Resolve-ContainedPath `
        -RootPath $ResolvedRepositoryRoot `
        -Path $TargetPath `
        -Description 'target ConfigMap'

    $SourceContent = [IO.File]::ReadAllText($ResolvedSourcePath)
    $CanonicalSource = ConvertTo-CanonicalJson -Content $SourceContent

    if ((Invoke-YqScalar -Expression '.kind' -Path $ResolvedTargetPath) -ne 'ConfigMap') {
        throw 'The target manifest is not a ConfigMap.'
    }
    if ((Invoke-YqScalar -Expression '.metadata.name' -Path $ResolvedTargetPath) -ne $ConfigMapName) {
        throw "The target ConfigMap is not named $ConfigMapName."
    }
    $DataExpression = ".data[`"$ConfigMapKey`"]"
    if ((Invoke-YqScalar -Expression ".data | has(`"$ConfigMapKey`")" -Path $ResolvedTargetPath) -ne 'true') {
        throw "The target ConfigMap does not contain data.$ConfigMapKey."
    }

    $RelativeTargetPath = [IO.Path]::GetRelativePath($ResolvedRepositoryRoot, $ResolvedTargetPath)
    $ExistingStatus = @(git -C $ResolvedRepositoryRoot status --porcelain -- $RelativeTargetPath)
    if ($LASTEXITCODE -ne 0) {
        throw 'Git status failed for the target ConfigMap.'
    }
    if ($ExistingStatus.Count -gt 0) {
        throw 'The target ConfigMap already has an uncommitted change.'
    }

    if (-not $PSCmdlet.ShouldProcess($RelativeTargetPath, "Synchronize $ConfigMapKey")) {
        return
    }

    $OriginalTargetBytes = [IO.File]::ReadAllBytes($ResolvedTargetPath)
    try {
        $env:APPSETTINGS_CONFIGMAP_SOURCE_PATH = $ResolvedSourcePath
        $WriteExpression = "$DataExpression = load_str(strenv(APPSETTINGS_CONFIGMAP_SOURCE_PATH)) | $DataExpression style=`"literal`""
        & $Yq.Source -i $WriteExpression $ResolvedTargetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'yq failed to update the target ConfigMap.'
        }

        $EmbeddedJsonString = & $Yq.Source -o=json -I=0 $DataExpression $ResolvedTargetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'yq failed to read the synchronized ConfigMap value.'
        }
        $EmbeddedContent = "$EmbeddedJsonString" | ConvertFrom-Json
        $CanonicalEmbedded = ConvertTo-CanonicalJson -Content $EmbeddedContent
        if ($CanonicalEmbedded -cne $CanonicalSource) {
            throw 'The synchronized ConfigMap value is not semantically equivalent to the source JSON.'
        }
    }
    catch {
        [IO.File]::WriteAllBytes($ResolvedTargetPath, $OriginalTargetBytes)
        throw
    }
    finally {
        Remove-Item Env:APPSETTINGS_CONFIGMAP_SOURCE_PATH -ErrorAction SilentlyContinue
    }

    Write-Host "Synchronized appsettings configuration into $RelativeTargetPath."
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($ApplicationRoot)) {
        $ApplicationRoot = (& git -C (Get-Location).Path rev-parse --show-toplevel 2>$null).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ApplicationRoot)) {
            throw 'ApplicationRoot is required when the current directory is not an application Git worktree.'
        }
    }
    else {
        $ApplicationRoot = [IO.Path]::GetFullPath($ApplicationRoot)
    }
    if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        $SourcePath = Join-Path $ApplicationRoot 'appsettings.Local.json'
    }
    if ([string]::IsNullOrWhiteSpace($DeployConfigPath)) {
        $DeployConfigPath = Join-Path $ApplicationRoot 'deploy.local.psd1'
    }

    if (Test-Path -LiteralPath $DeployConfigPath) {
        $DeployConfig = Import-PowerShellDataFile -LiteralPath $DeployConfigPath
        if ([string]::IsNullOrWhiteSpace($ManifestRepo) -and $DeployConfig.ContainsKey('ManifestRepo')) {
            $ManifestRepo = $DeployConfig.ManifestRepo
        }
        if ([string]::IsNullOrWhiteSpace($AppSettingsConfigMapPath) -and
            $DeployConfig.ContainsKey('AppSettingsConfigMapPath')) {
            $AppSettingsConfigMapPath = $DeployConfig.AppSettingsConfigMapPath
        }
        if ([string]::IsNullOrWhiteSpace($AppSettingsConfigMapName) -and
            $DeployConfig.ContainsKey('AppSettingsConfigMapName')) {
            $AppSettingsConfigMapName = $DeployConfig.AppSettingsConfigMapName
        }
        if ($DeployConfig.ContainsKey('AppSettingsConfigMapKey') -and
            -not $PSBoundParameters.ContainsKey('AppSettingsConfigMapKey')) {
            $AppSettingsConfigMapKey = $DeployConfig.AppSettingsConfigMapKey
        }
    }

    if ([string]::IsNullOrWhiteSpace($ManifestRepo)) {
        throw 'ManifestRepo is required in deploy.local.psd1 or as a parameter.'
    }
    if ([string]::IsNullOrWhiteSpace($AppSettingsConfigMapPath)) {
        throw 'AppSettingsConfigMapPath is required in deploy.local.psd1 or as a parameter.'
    }
    if ([string]::IsNullOrWhiteSpace($AppSettingsConfigMapName)) {
        throw 'AppSettingsConfigMapName is required in deploy.local.psd1 or as a parameter.'
    }
    if ($AppSettingsConfigMapName -notmatch '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$') {
        throw 'AppSettingsConfigMapName is not a valid Kubernetes resource name.'
    }
    if ($AppSettingsConfigMapKey -notmatch '^[A-Za-z0-9._-]+$') {
        throw 'AppSettingsConfigMapKey contains unsupported characters.'
    }

    $TargetPath = Join-Path $ManifestRepo $AppSettingsConfigMapPath

    # PowerShell decodes native command output using the console output encoding, which on Windows
    # defaults to an OEM code page; that mangles any non-ASCII setting on the way back from yq and
    # fails the equivalence check.
    $PreviousOutputEncoding = [Console]::OutputEncoding
    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    try {
        Sync-AppSettingsToConfigMap `
            -SourcePath $SourcePath `
            -SourceRoot $ApplicationRoot `
            -TargetPath $TargetPath `
            -RepositoryRoot $ManifestRepo `
            -ConfigMapName $AppSettingsConfigMapName `
            -ConfigMapKey $AppSettingsConfigMapKey `
            -WhatIf:$WhatIfPreference `
            -Confirm:$false
    }
    finally {
        [Console]::OutputEncoding = $PreviousOutputEncoding
    }
}
