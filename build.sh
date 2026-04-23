#!/usr/bin/env bash
set -euo pipefail

REGISTRY="ghcr.io/f2calv"
GIT_REPOSITORY=$(basename "$(git rev-parse --show-toplevel)")
GIT_BRANCH=$(git branch --show-current)
GIT_COMMIT=$(git rev-parse HEAD)
GIT_TAG="latest-dev"

GITHUB_WORKFLOW="n/a"
GITHUB_RUN_ID=0
GITHUB_RUN_NUMBER=0

PLATFORMS="linux/amd64,linux/arm64,linux/arm/v7"
BUILDER_NAME="smarthaus1"

build() {
  local image_name="${1,,}"
  local workload_name="$2"
  local configuration="${3:-Debug}"
  local dockerfile="Dockerfile"
  if [ "$configuration" = "Debug" ]; then
    dockerfile="Dockerfile.Debug"
  fi

  docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1 \
    || docker buildx create --name "$BUILDER_NAME"
  docker buildx use "$BUILDER_NAME"

  docker buildx build \
    -t "${REGISTRY}/${image_name}:${GIT_TAG}" \
    -f "$dockerfile" \
    --build-arg WORKLOAD="$workload_name" \
    --build-arg CONFIGURATION="$configuration" \
    --build-arg GIT_REPOSITORY="$GIT_REPOSITORY" \
    --build-arg GIT_BRANCH="$GIT_BRANCH" \
    --build-arg GIT_COMMIT="$GIT_COMMIT" \
    --build-arg GIT_TAG="$GIT_TAG" \
    --build-arg GITHUB_WORKFLOW="$GITHUB_WORKFLOW" \
    --build-arg GITHUB_RUN_ID="$GITHUB_RUN_ID" \
    --build-arg GITHUB_RUN_NUMBER="$GITHUB_RUN_NUMBER" \
    --platform "$PLATFORMS" \
    --pull \
    .
}

build "workload" "CasCap.App.Server"
