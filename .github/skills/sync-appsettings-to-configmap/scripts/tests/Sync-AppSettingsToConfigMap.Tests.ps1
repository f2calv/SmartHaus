#Requires -Modules @{ ModuleName = 'Pester'; RequiredVersion = '5.7.1' }

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot '..\Sync-AppSettingsToConfigMap.ps1'
    . $script:ScriptPath
}

Describe 'Sync-AppSettingsToConfigMap' -Tag 'Unit' {
    BeforeEach {
        $script:SourceRoot = Join-Path $TestDrive 'smarthaus'
        $script:RepositoryRoot = Join-Path $TestDrive 'gitops'
        $script:SourcePath = Join-Path $script:SourceRoot 'appsettings.Local.json'
        $script:TargetPath = Join-Path $script:RepositoryRoot 'haus-appsettings.yaml'

        $null = New-Item -ItemType Directory -Path $script:SourceRoot -Force
        $null = New-Item -ItemType Directory -Path $script:RepositoryRoot -Force
        @'
{
  // Synthetic private configuration
  "AppConfig": {
    "Endpoint": "https://example.com",
  }
}
'@ | Set-Content -LiteralPath $script:SourcePath
        @'
apiVersion: v1
kind: ConfigMap
metadata:
  name: haus-appsettings
data:
  appsettings.Local.json: |-
    {
      "AppConfig": {
        "Endpoint": "https://old.example.com"
      }
    }
'@ | Set-Content -LiteralPath $script:TargetPath

        git -C $script:RepositoryRoot init --quiet
        git -C $script:RepositoryRoot config user.email test@example.com
        git -C $script:RepositoryRoot config user.name 'Synchronization Test'
        git -C $script:RepositoryRoot add -- haus-appsettings.yaml
        git -C $script:RepositoryRoot commit --quiet -m 'test fixture'
    }

    It 'validates without changing the target in WhatIf mode' {
        $Before = [IO.File]::ReadAllText($script:TargetPath)

        Sync-AppSettingsToConfigMap `
            -SourcePath $script:SourcePath `
            -SourceRoot $script:SourceRoot `
            -TargetPath $script:TargetPath `
            -RepositoryRoot $script:RepositoryRoot `
            -ConfigMapName haus-appsettings `
            -ConfigMapKey appsettings.Local.json `
            -WhatIf `
            -Confirm:$false

        [IO.File]::ReadAllText($script:TargetPath) | Should -BeExactly $Before
        @(git -C $script:RepositoryRoot status --porcelain).Count | Should -Be 0
    }

    It 'updates only the ConfigMap data value with semantically equivalent JSON' {
        Sync-AppSettingsToConfigMap `
            -SourcePath $script:SourcePath `
            -SourceRoot $script:SourceRoot `
            -TargetPath $script:TargetPath `
            -RepositoryRoot $script:RepositoryRoot `
            -ConfigMapName haus-appsettings `
            -ConfigMapKey appsettings.Local.json `
            -Confirm:$false

        $EmbeddedJsonString = & yq -o=json -I=0 '.data."appsettings.Local.json"' $script:TargetPath
        $Embedded = "$EmbeddedJsonString" | ConvertFrom-Json
        $Parsed = $Embedded | ConvertFrom-Json

        $Parsed.AppConfig.Endpoint | Should -BeExactly 'https://example.com'
        @(git -C $script:RepositoryRoot status --porcelain).Count | Should -Be 1
        (git -C $script:RepositoryRoot status --porcelain) | Should -Match 'haus-appsettings.yaml'
        Test-Path Env:APPSETTINGS_CONFIGMAP_SOURCE_PATH | Should -BeFalse
    }

    It 'refuses to overwrite an existing target change' {
        Add-Content -LiteralPath $script:TargetPath -Value '# existing change'

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*already has an uncommitted change*'
    }

    It 'rejects credential-bearing <PropertyName> values without changing the target' -TestCases @(
        @{ PropertyName = 'ApiKey' }
        @{ PropertyName = 'AuthKey' }
        @{ PropertyName = 'DeviceToken' }
        @{ PropertyName = 'VendorKey' }
        @{ PropertyName = 'Token' }
        @{ PropertyName = 'Secret' }
        @{ PropertyName = 'SasToken' }
        @{ PropertyName = 'ClientKey' }
        @{ PropertyName = 'ApiKeys' }
        @{ PropertyName = 'AccessKey' }
        @{ PropertyName = 'SecretAccessKey' }
        @{ PropertyName = 'Authorization' }
        @{ PropertyName = 'SigningKey' }
        @{ PropertyName = 'EncryptionKey' }
        @{ PropertyName = 'SasKey' }
        @{ PropertyName = 'BasicAuth' }
        @{ PropertyName = 'AuthorizationHeader' }
        @{ PropertyName = 'SecretKey' }
        @{ PropertyName = 'SubscriptionKey' }
        @{ PropertyName = 'SharedSecret' }
    ) {
        param($PropertyName)

        [IO.File]::WriteAllText(
            $script:SourcePath,
            "{`n  `"$PropertyName`": `"not-a-placeholder`"`n}`n")
        $Before = [IO.File]::ReadAllText($script:TargetPath)

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*Credential-bearing configuration is not allowed*'

        [IO.File]::ReadAllText($script:TargetPath) | Should -BeExactly $Before
        @(git -C $script:RepositoryRoot status --porcelain).Count | Should -Be 0
    }

    It 'rejects a target outside the configured GitOps repository' {
        $OutsideTarget = Join-Path $TestDrive 'outside.yaml'
        Copy-Item -LiteralPath $script:TargetPath -Destination $OutsideTarget

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $OutsideTarget `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*target ConfigMap must be inside its configured repository*'
    }

    It 'rejects secret-bearing connection format <Format>' -TestCases @(
        @{ Format = 'key-value'; Value = 'Host=example.com;Password=secret' }
        @{ Format = 'key-value-spaces'; Value = 'Host=example.com;Password = secret' }
        @{ Format = 'client-secret'; Value = 'Endpoint=example.com;ClientSecret=secret' }
        @{ Format = 'client-secret-snake'; Value = 'Endpoint=example.com;client_secret=secret' }
        @{ Format = 'embedded-secret'; Value = 'Endpoint=example.com;Secret = secret' }
        @{ Format = 'postgres-uri'; Value = 'postgresql://user:secret@example.com/database' }
        @{ Format = 'redis-uri'; Value = 'rediss://:secret@example.com:6379' }
        @{ Format = 'query-token'; Value = 'https://example.com/path?token=secret' }
    ) {
        param($Format, $Value)

        [IO.File]::WriteAllText(
            $script:SourcePath,
            "{`n  `"ConnectionStrings`": {`n    `"Default`": `"$Value`"`n  }`n}`n")

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*Credential-bearing configuration is not allowed*'

        @(git -C $script:RepositoryRoot status --porcelain).Count | Should -Be 0
    }

    It 'rejects a source outside the SmartHaus repository' {
        $OutsideSource = Join-Path $TestDrive 'outside.json'
        Copy-Item -LiteralPath $script:SourcePath -Destination $OutsideSource

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $OutsideSource `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*source appsettings file must be inside its configured repository*'
    }

    It 'rejects secrets nested below credential container <ContainerName>' -TestCases @(
        @{ ContainerName = 'ApiKeys'; ChildName = 'Service' }
        @{ ContainerName = 'Credentials'; ChildName = 'Pass' }
    ) {
        param($ContainerName, $ChildName)

        [IO.File]::WriteAllText(
            $script:SourcePath,
            "{`n  `"$ContainerName`": {`n    `"$ChildName`": `"secret`"`n  }`n}`n")

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*Credential-bearing configuration is not allowed*'
    }

    It 'rejects secrets nested below a credential-named array' {
        [IO.File]::WriteAllText(
            $script:SourcePath,
            "{`n  `"Tokens`": [`"secret`"]`n}`n")

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*Credential-bearing configuration is not allowed*'
    }

    It 'rejects secret-form strings inside an ordinary array' {
        [IO.File]::WriteAllText(
            $script:SourcePath,
            "{`n  `"Endpoints`": [`"postgresql://user:secret@example.com/database`"]`n}`n")

        {
            Sync-AppSettingsToConfigMap `
                -SourcePath $script:SourcePath `
                -SourceRoot $script:SourceRoot `
                -TargetPath $script:TargetPath `
                -RepositoryRoot $script:RepositoryRoot `
                -ConfigMapName haus-appsettings `
                -ConfigMapKey appsettings.Local.json `
                -Confirm:$false
        } | Should -Throw '*Credential-bearing configuration is not allowed*'
    }
}
