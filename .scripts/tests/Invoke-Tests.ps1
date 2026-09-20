#!/usr/bin/env pwsh
#Requires -Version 7.4
<#
.SYNOPSIS
    Runs the build and deploy PowerShell unit tests.
.DESCRIPTION
    Imports Pester 5.7.1 and runs the adjacent test files with detailed output.
    Returns a nonzero exit code when any test fails.
.PARAMETER TestPath
    Test file or directory to run. Defaults to this tests directory.
.EXAMPLE
    ./.scripts/tests/Invoke-Tests.ps1
.EXAMPLE
    ./.scripts/tests/Invoke-Tests.ps1 -TestPath ./.scripts/tests/build.Tests.ps1
#>
[CmdletBinding()]
param(
    [string]$TestPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Invoke-ScriptTests {
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory)][string]$Path)

    Import-Module Pester -RequiredVersion 5.7.1 -ErrorAction Stop
    $configuration = [PesterConfiguration]::Default
    $configuration.Run.Path = $Path
    $configuration.Run.PassThru = $true
    $configuration.Output.Verbosity = 'Detailed'
    $result = Invoke-Pester -Configuration $configuration
    if ($result.FailedCount -gt 0) { return 1 }
    return 0
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        exit (Invoke-ScriptTests -Path $TestPath)
    }
    catch {
        Write-Error -ErrorAction Continue "PowerShell tests failed to start: $($_.Exception.Message)"
        exit 1
    }
}
