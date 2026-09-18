#Requires -Version 7.4
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.7.1' }

BeforeAll {
    . (Join-Path $PSScriptRoot '../../build.ps1')
    function git { throw 'Unmocked git invocation.' }
    function docker { throw 'Unmocked docker invocation.' }
    function dotnet-gitversion { '2.3.4-BETA.1' }
}

Describe 'build.ps1 helpers' -Tag 'Unit' {
    Context 'dependency discovery' {
        It 'Returns unique sorted sibling repository names from Dockerfile COPY instructions' {
            $dockerfile = Join-Path $TestDrive 'Dockerfile.Debug'
            @'
COPY deps/Zeta /src/Zeta
COPY deps/Alpha /src/Alpha
COPY deps/Zeta /src/Zeta
'@ | Set-Content -LiteralPath $dockerfile

            @(Get-DependencyRepositories -DockerfilePath $dockerfile) | Should -Be @('Alpha', 'Zeta')
        }

        It 'Rejects a Dockerfile without sibling dependencies' {
            $dockerfile = Join-Path $TestDrive 'Dockerfile.Debug'
            'FROM example.invalid/runtime:latest' | Set-Content -LiteralPath $dockerfile

            { Get-DependencyRepositories -DockerfilePath $dockerfile } | Should -Throw '*No sibling dependencies*'
        }
    }

    Context 'Dockerfile selection' {
        It 'Selects Dockerfile.Debug for Debug builds' {
            Get-BuildDockerfile -Configuration Debug | Should -BeExactly 'Dockerfile.Debug'
        }

        It 'Selects Dockerfile for Release builds' {
            Get-BuildDockerfile -Configuration Release | Should -BeExactly 'Dockerfile'
        }
    }

    Context 'tag resolution' {
        It 'Normalizes an explicit tag' {
            Resolve-Tag -ExplicitTag 'Feature.Build-7' -RepositoryRoot $TestDrive | Should -BeExactly 'feature.build-7'
        }

        It 'Uses latest-dev for a non-push build without an explicit tag' {
            Resolve-Tag -RepositoryRoot $TestDrive | Should -BeExactly 'latest-dev'
        }

        It 'Normalizes the GitVersion tag for a push build' {
            Resolve-Tag -Push -RepositoryRoot $TestDrive | Should -BeExactly '2.3.4-beta.1'
        }
    }

    Context 'WhatIf behavior' {
        BeforeEach {
            Mock Get-DependencyRepositories { @('Synthetic.Dependency') }
            Mock Sync-Deps { throw 'Sync-Deps must not run under WhatIf.' }
            Mock Resolve-Tag { throw 'Resolve-Tag must not run under WhatIf.' }
            Mock Connect-Ghcr { throw 'Connect-Ghcr must not run under WhatIf.' }
            Mock git { throw 'git must not run under WhatIf.' }
            Mock docker { throw 'docker must not run under WhatIf.' }
        }

        It 'Does not invoke mutating or external operations' {
            Invoke-Build -WhatIf

            Should -Invoke Sync-Deps -Times 0 -Exactly
            Should -Invoke Resolve-Tag -Times 0 -Exactly
            Should -Invoke Connect-Ghcr -Times 0 -Exactly
            Should -Invoke git -Times 0 -Exactly
            Should -Invoke docker -Times 0 -Exactly
        }
    }
}
