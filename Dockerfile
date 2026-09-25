# syntax=docker/dockerfile:1
#
# Multi-architecture image, built from a single Dockerfile. Structure follows
# https://github.com/f2calv/multi-arch-container-dotnet
#
# One image serves every SmartHaus workload; the WORKLOAD build argument selects which project
# is published, and the matching runtime environment variable selects which assembly is started.
#
# ------------------------------------------------------------------------------
# Stage 1 of 3: build
#
# Pinned to $BUILDPLATFORM and CROSS-COMPILES to $TARGETPLATFORM; emulating the
# target under QEMU instead is often an order of magnitude slower.
# ------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]

ARG WORKLOAD=CasCap.App.Server
ARG CONFIGURATION=Release

# -- Dependency layer ----------------------------------------------------------
# Cached until a csproj/props or package version changes. Copy every project manifest first
# (--parents preserves directory structure) so editing source (.cs) files reuses the cached
# restore; appsettings.json arrives with the sources because restore never reads it. Restore is
# platform-agnostic, so keep it before ARG TARGETARCH to share it across architectures, and
# restore every runtime identifier so each platform's publish runs offline with --no-restore.
# Configuration is passed because Release and Debug resolve different package references.
COPY --parents src/**/*.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "src/$WORKLOAD/$WORKLOAD.csproj" -p:Configuration="$CONFIGURATION" \
    "-p:RuntimeIdentifiers=\"linux-x64;linux-arm64;linux-arm\""

# -- Compile layer -------------------------------------------------------------
COPY . .

# buildx injects TARGETARCH/TARGETVARIANT automatically:
#   linux/amd64 -> amd64, linux/arm64 -> arm64, linux/arm/v7 -> arm + v7
# Concatenating the two gives a single flat token to switch on. The publish only reads packages
# the restore already wrote, so the platform legs share the cache and need no network.
ARG TARGETARCH
ARG TARGETVARIANT
RUN --network=none --mount=type=cache,target=/root/.nuget/packages,sharing=shared <<EOF
set -eux
# https://learn.microsoft.com/dotnet/core/rid-catalog
case "${TARGETARCH}${TARGETVARIANT}" in
    amd64) RID=linux-x64   ;;
    arm64) RID=linux-arm64 ;;
    armv7) RID=linux-arm   ;;
    *) echo "unsupported platform: linux/${TARGETARCH}/${TARGETVARIANT}" >&2; exit 1 ;;
esac
dotnet publish "src/$WORKLOAD/$WORKLOAD.csproj" -c "$CONFIGURATION" -o /app/publish -r "$RID" \
    --self-contained false --no-restore
EOF

# ------------------------------------------------------------------------------
# Stage 2 of 3: runtime
#
# No --platform override here, so buildx resolves the base image for
# $TARGETPLATFORM and the resulting image is genuinely native to the target.
#
# Alternatives, smallest to largest:
#   mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled  no shell and no apt, so the helper
#                                                        scripts and libgpiod cannot be installed
#   mcr.microsoft.com/dotnet/aspnet:10.0-alpine          musl, has a shell, no tzdata by default
#   mcr.microsoft.com/dotnet/aspnet:10.0                 full Ubuntu, largest (used here)
#
# The full Ubuntu image is required: workloads need apt-installable runtime dependencies
# (libgpiod) and a shell for wait-for-it.sh / ffmpeg-record.sh. It already ships tzdata.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# -- Runtime dependencies ------------------------------------------------------
# Installed before the application layers so editing source never re-runs this install.
# ffmpeg is required, not optional: voice-message speech-to-text normalises every inbound
# attachment through it, and ffmpeg-record.sh depends on it too.
# libgpiod2t64 ships only libgpiod.so.2, but System.Device.Gpio P/Invokes the unversioned
# "libgpiod" and .NET never probes a versioned soname, so the symlink that libgpiod-dev would
# otherwise supply is recreated here rather than pulling headers and static libs into the image.
RUN <<EOF
set -eux
apt-get update
apt-get install -y --no-install-recommends curl libgpiod2t64 ffmpeg
for lib in /usr/lib/*/libgpiod.so.2; do
    ln -sf "$(basename "$lib")" "$(dirname "$lib")/libgpiod.so"
done
rm -rf /var/lib/apt/lists/*
ffmpeg -version
test -e "$(dirname "$(ls /usr/lib/*/libgpiod.so.2)")/libgpiod.so"
EOF

COPY --link --from=build /app/publish .
COPY ["wait-for-it.sh", "ffmpeg-record.sh", "./"]

# -- Provenance ----------------------------------------------------------------
# Supplied by the CI workflow (.github/workflows/ci.yml) or by build.ps1/build.sh.
ARG GIT_REPOSITORY=n/a
ENV GIT_REPOSITORY=$GIT_REPOSITORY
ARG GIT_BRANCH=n/a
ENV GIT_BRANCH=$GIT_BRANCH
ARG GIT_COMMIT=n/a
ENV GIT_COMMIT=$GIT_COMMIT
ARG GIT_TAG=n/a
ENV GIT_TAG=$GIT_TAG

ARG GITHUB_WORKFLOW=n/a
ENV GITHUB_WORKFLOW=$GITHUB_WORKFLOW
ARG GITHUB_RUN_ID=0
ENV GITHUB_RUN_ID=$GITHUB_RUN_ID
ARG GITHUB_RUN_NUMBER=0
ENV GITHUB_RUN_NUMBER=$GITHUB_RUN_NUMBER

EXPOSE 8080
ARG WORKLOAD=CasCap.App.Server
ENV WORKLOAD=$WORKLOAD

# https://github.com/opencontainers/image-spec/blob/main/annotations.md
LABEL org.opencontainers.image.title="$WORKLOAD" \
    org.opencontainers.image.description="SmartHaus service-orientated smart home workload" \
    org.opencontainers.image.source="https://github.com/f2calv/SmartHaus" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.version="$GIT_TAG" \
    org.opencontainers.image.revision="$GIT_COMMIT"

USER $APP_UID

# exec replaces the shell so dotnet becomes PID 1 and receives SIGTERM for a clean shutdown.
ENTRYPOINT ["sh", "-c", "exec dotnet ${WORKLOAD}.dll"]

# ------------------------------------------------------------------------------
# Optional stage: debug
#
# Investigation tooling, never published. `final` does not derive from it, so it is
# built only on request:
#
#   docker buildx build --target debug --platform linux/arm64 -t smarthaus:debug .
#
# AzCopy is pinned to a release and verified against the SHA-256 digest GitHub
# publishes for each asset. Microsoft publishes amd64 and arm64 only, so this
# target fails on linux/arm/v7.
# ------------------------------------------------------------------------------
FROM runtime AS debug
USER root
ARG TARGETARCH
ARG AZCOPY_VERSION=10.32.8
RUN <<EOF
set -eux
case "$TARGETARCH" in
    amd64) SHA256=a95277dbc265912cefdddbaf251aa99ec648cb18ba657e8788066357a9022dc3 ;;
    arm64) SHA256=50e6e58a109f2afd64376b5b003973ff213fbfdec795fe81a95b208982061d9b ;;
    *) echo "azcopy publishes no build for linux/$TARGETARCH" >&2; exit 1 ;;
esac
curl -fsSL --proto '=https' --proto-redir '=https' -o /tmp/azcopy.tar.gz \
    "https://github.com/Azure/azure-storage-azcopy/releases/download/v${AZCOPY_VERSION}/azcopy_linux_${TARGETARCH}_${AZCOPY_VERSION}.tar.gz"
echo "${SHA256}  /tmp/azcopy.tar.gz" | sha256sum -c -
tar -xzf /tmp/azcopy.tar.gz -C /tmp --strip-components=1 --wildcards '*/azcopy'
install -m 0755 /tmp/azcopy /usr/local/bin/azcopy
rm -f /tmp/azcopy /tmp/azcopy.tar.gz
azcopy --version
EOF
USER $APP_UID

# ------------------------------------------------------------------------------
# Stage 3 of 3: final
#
# The published image: the runtime stage without the debug tooling.
# ------------------------------------------------------------------------------
FROM runtime AS final
