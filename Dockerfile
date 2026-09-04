# syntax=docker/dockerfile:1
# check=skip=CopyIgnoredFile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["Directory.Build.props", "Directory.Packages.props", "appsettings.json", "./"]

ARG WORKLOAD=CasCap.App.Server
ARG CONFIGURATION=Release

# ── Restore layer (cached until a csproj/props or package version changes) ──
# Copy every project manifest first (--parents preserves directory structure) so
# editing source (.cs) files reuses the cached restore. Restore is platform-agnostic,
# so keep it before ARG TARGETPLATFORM to share it across architectures.
COPY --parents src/**/*.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "src/$WORKLOAD/$WORKLOAD.csproj"
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

#FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final #missing tzdata?
#FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["wait-for-it.sh", "ffmpeg-record.sh", "./"]
#RUN apt-get update && apt-get install -yq tzdata ffmpeg

RUN apt-get update \
    && apt-get install -y curl libgpiod-dev

# Install AzCopy
#RUN cd /tmp && \
#    curl -sL https://aka.ms/downloadazcopy-v10-linux -o azcopy.tar.gz && \
#    tar -xzf azcopy.tar.gz --strip-components=1 && \
#    mv azcopy /usr/local/bin/ && \
#    chmod +x /usr/local/bin/azcopy && \
#    rm -rf /tmp/*

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

ENTRYPOINT ["sh", "-c", "dotnet ${WORKLOAD}.dll"]
