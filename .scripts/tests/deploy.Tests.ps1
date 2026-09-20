#Requires -Version 7.4
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.7.1' }

BeforeAll {
    . (Join-Path $PSScriptRoot '../../deploy.ps1')
    function git { throw 'Unmocked git invocation.' }
    function yq { throw 'Unmocked yq invocation.' }
    function helm { throw 'Unmocked helm invocation.' }
    function dotnet { throw 'Unmocked dotnet invocation.' }
}

Describe 'deploy.ps1 helpers' -Tag 'Unit' {
    Context 'build argument validation' {
        It 'Keeps the configured platform scalar when invoking the image build' {
            $parameter = (Get-Command Invoke-DeploymentImageBuild).Parameters['Platforms']

            $parameter.ParameterType | Should -Be ([string])
        }

        It 'Removes a redundant Debug configuration and preserves other arguments' {
            @(ConvertTo-DeployBuildArguments -Arguments @('-Configuration', 'Debug', '-WorkloadName', 'Synthetic.Workload')) |
            Should -Be @('-WorkloadName', 'Synthetic.Workload')
        }

        It 'Accepts the colon form of Debug configuration' {
            @(ConvertTo-DeployBuildArguments -Arguments @('-Configuration:Debug', '-Push')) | Should -Be @('-Push')
        }

        It 'Rejects Release because deploy is Debug-only' {
            { ConvertTo-DeployBuildArguments -Arguments @('-Configuration', 'Release') } | Should -Throw '*only performs Debug builds*'
        }
    }

    Context 'manifest selection and validation' {
        It 'Selects the application manifest for a normal deployment' {
            Resolve-DeploymentManifestPath -ManifestPath 'apps/application.yaml' -DashboardManifestPath 'apps/dashboard.yaml' |
            Should -BeExactly 'apps/application.yaml'
        }

        It 'Selects the dashboard manifest for a charts-only deployment' {
            Resolve-DeploymentManifestPath -OnlyCharts -ManifestPath 'apps/application.yaml' -DashboardManifestPath 'apps/dashboard.yaml' |
            Should -BeExactly 'apps/dashboard.yaml'
        }

        It 'Requires the mode-specific manifest path' {
            { Resolve-DeploymentManifestPath -OnlyCharts -ManifestPath 'apps/application.yaml' } |
            Should -Throw '*DashboardManifestPath*'
        }
    }

    Context 'manifest patch decisions' {
        It 'Omits chart targetRevision when no chart is published' {
            Get-ManifestPatchExpression | Should -Not -Match 'targetRevision'
        }

        It 'Includes chart targetRevision when a chart is published' {
            Get-ManifestPatchExpression -Chart | Should -Match 'targetRevision = strenv\(DEPLOY_CHART_VERSION\)'
        }

        It 'Always patches image identity, pull policy, and shared and alias-local rollout annotations' {
            $expression = Get-ManifestPatchExpression

            $expression | Should -Match 'DEPLOY_IMG_REPO'
            $expression | Should -Match 'DEPLOY_IMG_TAG'
            $expression | Should -Match 'pullPolicy = "Always"'
            $expression | Should -Match '_shared\.podAnnotations'
            $expression | Should -Match 'select\(has\("podAnnotations"\)\)'
        }
    }

    Context 'WhatIf behavior' {
        BeforeEach {
            $script:ManifestRepo = Join-Path $TestDrive 'gitops'
            $script:ManifestPath = 'apps/application.yaml'
            $script:DashboardManifestPath = $null
            $script:DeployConfigPath = Join-Path $TestDrive 'missing.psd1'
            $script:PodAnnotationName = 'example.invalid/deployed-version'
            $script:OnlyCharts = $false
            $script:Rest = @()
            New-Item -ItemType Directory -Path (Join-Path $script:ManifestRepo 'apps') -Force | Out-Null
            $script:ManifestFile = Join-Path $script:ManifestRepo $script:ManifestPath
            'spec: {}' | Set-Content -LiteralPath $script:ManifestFile

            Mock git { throw 'git must not run under WhatIf.' }
            Mock yq { throw 'yq must not run under WhatIf.' }
            Mock helm { throw 'helm must not run under WhatIf.' }
            Mock dotnet { throw 'dotnet must not run under WhatIf.' }
        }

        It 'Validates synthetic inputs without changing the manifest or invoking external tools' {
            $before = Get-Content -LiteralPath $script:ManifestFile -Raw

            Invoke-Deployment -CallerBoundParameters @{
                ManifestRepo      = $script:ManifestRepo
                ManifestPath      = $script:ManifestPath
                PodAnnotationName = $script:PodAnnotationName
            } -WhatIf

            Get-Content -LiteralPath $script:ManifestFile -Raw | Should -BeExactly $before
            Should -Invoke git -Times 0 -Exactly
            Should -Invoke yq -Times 0 -Exactly
            Should -Invoke helm -Times 0 -Exactly
            Should -Invoke dotnet -Times 0 -Exactly
        }
    }
}
