#!/usr/bin/env bash
# Builds the application container image for local validation or publication.
#   Debug   -> Dockerfile.Debug, mirroring the sibling repositories it declares into deps/
#   Release -> Dockerfile
# Push builds authenticate to GHCR and tag with GitVersion FullSemVer unless --tag is supplied.
# Repository-specific values are derived, so this script is identical across repositories.
set -euo pipefail

REPO_ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)

# ── Defaults ────────────────────────────────────────────────────────────────
REGISTRY="ghcr.io/f2calv"
PUSH=false
CONFIGURATION="Debug"          # Debug -> Dockerfile.Debug (+ local sibling deps); Release -> Dockerfile
TAG=""                         # override; default GitVersion FullSemVer when --push, else latest-dev
PLATFORMS="linux/amd64,linux/arm64,linux/arm/v7"
IMAGE_NAME=$(basename "$REPO_ROOT")
IMAGE_NAME="${IMAGE_NAME,,}"   # image repository name defaults to the repository directory name
WORKLOAD="CasCap.App.Server"

usage() {
  cat <<EOF
Usage: build.sh [options]
  --push                 Authenticate via gh CLI and push to ghcr.io (default: local build only)
  --config <cfg>         Debug | Release (default: ${CONFIGURATION})
  --tag <tag>            Image tag override (default: GitVersion when --push, else latest-dev)
  --platforms <list>     Target platform(s), e.g. linux/arm64 (default: ${PLATFORMS})
  --image <name>         Image repository name under ${REGISTRY} (default: ${IMAGE_NAME})
  --workload <name>      Build-arg WORKLOAD (default: ${WORKLOAD})
EOF
}

# ── Arg parsing ─────────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --push) PUSH=true; shift ;;
    --config|--configuration) CONFIGURATION="$2"; shift 2 ;;
    --tag) TAG="$2"; shift 2 ;;
    --platforms) PLATFORMS="$2"; shift 2 ;;
    --image) IMAGE_NAME="$2"; shift 2 ;;
    --workload) WORKLOAD="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
  esac
done

case "$CONFIGURATION" in
  Debug|Release) ;;
  *) echo "Unknown configuration: ${CONFIGURATION} (expected Debug or Release)." >&2; exit 1 ;;
esac

IMAGE_NAME="${IMAGE_NAME,,}"
BUILDER_NAME="${IMAGE_NAME}1"

GIT_REPOSITORY=$(basename "$REPO_ROOT")
GIT_BRANCH=$(git -C "$REPO_ROOT" branch --show-current)
GIT_COMMIT=$(git -C "$REPO_ROOT" rev-parse HEAD)

GITHUB_WORKFLOW="local"
GITHUB_RUN_ID=0
GITHUB_RUN_NUMBER=0

# Sibling repositories are declared by Dockerfile.Debug and mirrored into deps/ for Debug
# builds, so a fix/feature can be verified on k8s without first pushing those repos to GitHub.
get_dep_repos() {
  local dockerfile="${REPO_ROOT}/Dockerfile.Debug"
  [[ -f "$dockerfile" ]] || { echo "Dockerfile.Debug not found at '${dockerfile}'." >&2; exit 1; }
  local repos
  repos=$(sed -nE 's#^[[:space:]]*COPY[[:space:]]+deps/([^/[:space:]]+)[[:space:]]+/.*#\1#p' "$dockerfile" | sort -u)
  [[ -n "$repos" ]] || { echo "No sibling dependencies were found in '${dockerfile}'." >&2; exit 1; }
  echo "$repos"
}

resolve_tag() {
  if [[ -n "$TAG" ]]; then
    echo "${TAG,,}"
  elif [[ "$PUSH" == true ]]; then
    if ! command -v dotnet-gitversion >/dev/null 2>&1; then
      echo "dotnet-gitversion not found. Installing GitVersion.Tool globally..." >&2
      dotnet tool install -g GitVersion.Tool >&2 \
        || { echo "Failed to install GitVersion.Tool. Run: dotnet tool install -g GitVersion.Tool" >&2; exit 1; }
      # Ensure the global tools path is on PATH for the current session.
      export PATH="${HOME}/.dotnet/tools:${PATH}"
    fi
    local t; t=$(dotnet-gitversion "$REPO_ROOT" /showvariable FullSemVer)
    echo "${t,,}"
  else
    echo "latest-dev"
  fi
}

connect_ghcr() {
  command -v gh >/dev/null 2>&1 || { echo "gh CLI not found. Install: https://cli.github.com" >&2; exit 1; }
  if ! gh auth status 2>&1 | grep -q "write:packages"; then
    echo "Refreshing gh auth to add the write:packages scope..."
    gh auth refresh -h github.com -s write:packages
  fi
  local gh_user; gh_user=$(gh api user --jq .login)
  echo "Authenticating Docker to ghcr.io as ${gh_user}..."
  gh auth token | docker login ghcr.io -u "$gh_user" --password-stdin
}

sync_deps() {
  command -v rsync >/dev/null 2>&1 || { echo "rsync not found (required to sync local sibling deps)." >&2; exit 1; }
  local parent; parent=$(dirname "$REPO_ROOT")
  local repo
  while IFS= read -r repo; do
    local src="${parent}/${repo}"
    [[ -d "$src" ]] || { echo "Debug build requires sibling repo '${repo}' at '${src}' (not found)." >&2; exit 1; }
    local dst="${REPO_ROOT}/deps/${repo}"
    case "${dst}/" in
      "${src}/"*) echo "Refusing to mirror '${src}' into its own descendant '${dst}'." >&2; exit 1 ;;
    esac
    echo "Syncing ${repo} -> deps/${repo}"
    mkdir -p "$dst"
    # Mirror (incremental). Exclude build output, VCS, and local-only secrets.
    rsync -a --delete \
      --exclude 'bin/' --exclude 'obj/' --exclude '.git/' --exclude '.vs/' \
      --exclude 'node_modules/' --exclude 'deps/' \
      --exclude 'appsettings.Local*.json' --exclude '*.user' \
      "${src}/" "${dst}/"
  done < <(get_dep_repos)
}

build() {
  local dockerfile="Dockerfile"
  [[ "$CONFIGURATION" == "Debug" ]] && dockerfile="Dockerfile.Debug"

  local tag_value; tag_value=$(resolve_tag)
  local img="${REGISTRY}/${IMAGE_NAME}:${tag_value}"

  [[ "$CONFIGURATION" == "Debug" ]] && sync_deps
  [[ "$PUSH" == true ]] && connect_ghcr

  docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1 \
    || docker buildx create --name "$BUILDER_NAME"
  docker buildx use "$BUILDER_NAME"

  # Multi-arch manifests cannot be loaded into the local engine; --push publishes,
  # --pull just validates the build (the original local-only behaviour).
  local publish_arg="--pull"
  [[ "$PUSH" == true ]] && publish_arg="--push"

  docker buildx build \
    -t "$img" \
    -f "${REPO_ROOT}/${dockerfile}" \
    --build-arg WORKLOAD="$WORKLOAD" \
    --build-arg CONFIGURATION="$CONFIGURATION" \
    --build-arg GIT_REPOSITORY="$GIT_REPOSITORY" \
    --build-arg GIT_BRANCH="$GIT_BRANCH" \
    --build-arg GIT_COMMIT="$GIT_COMMIT" \
    --build-arg GIT_TAG="$tag_value" \
    --build-arg GITHUB_WORKFLOW="$GITHUB_WORKFLOW" \
    --build-arg GITHUB_RUN_ID="$GITHUB_RUN_ID" \
    --build-arg GITHUB_RUN_NUMBER="$GITHUB_RUN_NUMBER" \
    --platform "$PLATFORMS" \
    "$publish_arg" \
    "$REPO_ROOT"

  if [[ "$PUSH" == true ]]; then
    echo "Pushed: ${img}"
  else
    echo "Built (not pushed): ${img}"
  fi
}

build
