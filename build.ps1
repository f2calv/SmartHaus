Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$REGISTRY = "ghcr.io/f2calv"
$REPO_ROOT = git rev-parse --show-toplevel
$GIT_REPOSITORY = $REPO_ROOT | Split-Path -Leaf
$GIT_BRANCH = $(git branch --show-current)
$GIT_COMMIT = $(git rev-parse HEAD)
$GIT_TAG = "latest-dev"

$GITHUB_WORKFLOW = "n/a"
$GITHUB_RUN_ID = 0
$GITHUB_RUN_NUMBER = 0

$PLATFORMS = "linux/amd64,linux/arm64,linux/arm/v7"
$BUILDER_NAME = "smarthaus1"

function Invoke-Build {
    param(
        [parameter(Mandatory = $true)][string][ValidateNotNullOrEmpty()]$ImageName,
        [parameter(Mandatory = $true)][string][ValidateNotNullOrEmpty()]$WorkloadName,
        [string]$Configuration = "Debug"
    )

    $dockerfile = if ($Configuration -eq "Debug") { "Dockerfile.Debug" } else { "Dockerfile" }
    $img = "$REGISTRY/$($ImageName.ToLower()):$GIT_TAG"

    & docker buildx inspect $BUILDER_NAME 2>$null
    if ($LASTEXITCODE -ne 0) {
        & docker buildx create --name $BUILDER_NAME
    }
    & docker buildx use $BUILDER_NAME

    & docker buildx build -t $img `
        -f $dockerfile `
        --build-arg WORKLOAD=$WorkloadName `
        --build-arg CONFIGURATION=$Configuration `
        --build-arg GIT_REPOSITORY=$GIT_REPOSITORY `
        --build-arg GIT_BRANCH=$GIT_BRANCH `
        --build-arg GIT_COMMIT=$GIT_COMMIT `
        --build-arg GIT_TAG=$GIT_TAG `
        --build-arg GITHUB_WORKFLOW=$GITHUB_WORKFLOW `
        --build-arg GITHUB_RUN_ID=$GITHUB_RUN_ID `
        --build-arg GITHUB_RUN_NUMBER=$GITHUB_RUN_NUMBER `
        --platform $PLATFORMS `
        --pull `
        .
    if ($LASTEXITCODE -ne 0) { throw "docker buildx build failed with exit code $LASTEXITCODE" }
}

Invoke-Build -ImageName "workload" -WorkloadName "CasCap.App.Server"

#kubectl config use-context k3s

#kubectl rollout restart deploy/buderuskm200
#kubectl rollout restart deploy/froniussymo
#kubectl rollout restart deploy/doorbird
#kubectl rollout restart deploy/knxbus

#kubectl get po -w
