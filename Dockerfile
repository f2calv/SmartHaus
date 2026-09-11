# syntax=docker/dockerfile:1
# check=skip=CopyIgnoredFile
#
# CopyIgnoredFile is skipped deliberately: .dockerignore re-excludes bin/ and obj/ beneath the
# deps/** allow-list that Dockerfile.Debug relies on, so the broad COPY instructions below
# legitimately step over ignored files.
#
# Multi-architecture image, built from a single Dockerfile. Structure follows
# https://github.com/f2calv/multi-arch-container-dotnet
#
# One image serves every SmartHaus workload; the WORKLOAD build argument selects which project
# is published, and the matching runtime environment variable selects which assembly is started.
#
# ------------------------------------------------------------------------------
# Stage 1 of 2: build
#
# Pinned to $BUILDPLATFORM and CROSS-COMPILES to $TARGETPLATFORM; emulating the
# target under QEMU instead is typically 10-50x slower.
# ------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["Directory.Build.props", "Directory.Packages.props", "appsettings.json", "./"]

ARG WORKLOAD=CasCap.App.Server
ARG CONFIGURATION=Release

# -- Dependency layer ----------------------------------------------------------
# Cached until a csproj/props or package version changes. Copy every project manifest first
# (--parents preserves directory structure) so editing source (.cs) files reuses the cached
# restore. Restore is platform-agnostic, so keep it before ARG TARGETARCH to share it across
# architectures.
COPY --parents src/**/*.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "src/$WORKLOAD/$WORKLOAD.csproj"

# -- Compile layer -------------------------------------------------------------
COPY . .

# buildx injects TARGETARCH/TARGETVARIANT automatically:
#   linux/amd64 -> amd64, linux/arm64 -> arm64, linux/arm/v7 -> arm + v7
# Concatenating the two gives a single flat token to switch on.
ARG TARGETARCH
ARG TARGETVARIANT
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked <<EOF
set -eux
# https://learn.microsoft.com/dotnet/core/rid-catalog
case "${TARGETARCH}${TARGETVARIANT}" in
    amd64) RID=linux-x64   ;;
    arm64) RID=linux-arm64 ;;
    armv7) RID=linux-arm   ;;
    *) echo "unsupported platform: linux/${TARGETARCH}/${TARGETVARIANT}" >&2; exit 1 ;;
esac
dotnet publish "src/$WORKLOAD/$WORKLOAD.csproj" -c "$CONFIGURATION" -o /app/publish -r "$RID" --self-contained false
EOF

# ------------------------------------------------------------------------------
# Stage 2 of 2: final
#
# No --platform override here, so buildx resolves the base image for
# $TARGETPLATFORM and the resulting image is genuinely native to the target.
#
# Alternatives, smallest to largest:
#   mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled  no shell and no apt, so the helper
#                                                        scripts and libgpiod cannot be installed
#   mcr.microsoft.com/dotnet/aspnet:10.0-alpine          musl, has a shell, no tzdata by default
#   mcr.microsoft.com/dotnet/aspnet:10.0                 full Debian, largest (used here)
#
# The full Debian image is required: workloads need apt-installable runtime dependencies
# (libgpiod) and a shell for wait-for-it.sh / ffmpeg-record.sh.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --link --from=build /app/publish .
COPY ["wait-for-it.sh", "ffmpeg-record.sh", "./"]

# -- Runtime dependencies ------------------------------------------------------
RUN <<EOF
set -eux
apt-get update
apt-get install -y --no-install-recommends curl libgpiod-dev
rm -rf /var/lib/apt/lists/*
EOF

# -- Temporary debug tooling ---------------------------------------------------
# Uncomment a block to add a debugging dependency to the image, then remove it again once the
# investigation is finished. Each block is kept build-ready so uncommenting is the only edit.
#
#RUN <<EOF
#set -eux
#apt-get update
#apt-get install -y --no-install-recommends tzdata ffmpeg
#rm -rf /var/lib/apt/lists/*
#EOF
#
# AzCopy. The aka.ms links are unversioned, so this always installs the current release.
# Microsoft publishes amd64 and arm64 only - there is no linux/arm/v7 build.
#ARG TARGETARCH
#RUN <<EOF
#set -eux
#case "$TARGETARCH" in
#    amd64) AZCOPY_URL=https://aka.ms/downloadazcopy-v10-linux ;;
#    arm64) AZCOPY_URL=https://aka.ms/downloadazcopy-v10-linux-arm64 ;;
#    *) echo "azcopy publishes no build for linux/$TARGETARCH" >&2; exit 1 ;;
#esac
#curl -fsSL "$AZCOPY_URL" -o /tmp/azcopy.tar.gz
#tar -xzf /tmp/azcopy.tar.gz -C /tmp --strip-components=1 --wildcards '*/azcopy'
#install -m 0755 /tmp/azcopy /usr/local/bin/azcopy
#rm -f /tmp/azcopy /tmp/azcopy.tar.gz
#EOF

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
EXPOSE 8081
ARG WORKLOAD=CasCap.App.Server
ENV WORKLOAD=$WORKLOAD

# https://github.com/opencontainers/image-spec/blob/main/annotations.md
LABEL org.opencontainers.image.title="$WORKLOAD" \
    org.opencontainers.image.description="SmartHaus service-orientated smart home workload" \
    org.opencontainers.image.source="https://github.com/f2calv/SmartHaus" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.version="$GIT_TAG" \
    org.opencontainers.image.revision="$GIT_COMMIT"

#TODO: run as a non-root USER. Pending confirmation that no workload needs root or a privileged
#      capability - knx/wiz/shelly attach to a Multus network and edge-* reads host hardware.

# exec replaces the shell so dotnet becomes PID 1 and receives SIGTERM for a clean shutdown.
ENTRYPOINT ["sh", "-c", "exec dotnet ${WORKLOAD}.dll"]
