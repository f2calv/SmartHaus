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
RUN dotnet restore "src/$WORKLOAD/$WORKLOAD.csproj"
COPY . .
ARG TARGETPLATFORM
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && dotnet publish "src/$WORKLOAD/$WORKLOAD.csproj" -c $CONFIGURATION -o /app/publish -r $RID --self-contained false

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

ARG GIT_REPOSITORY
ENV GIT_REPOSITORY=$GIT_REPOSITORY
ARG GIT_BRANCH
ENV GIT_BRANCH=$GIT_BRANCH
ARG GIT_COMMIT
ENV GIT_COMMIT=$GIT_COMMIT
ARG GIT_TAG
ENV GIT_TAG=$GIT_TAG

ARG GITHUB_WORKFLOW
ENV GITHUB_WORKFLOW=$GITHUB_WORKFLOW
ARG GITHUB_RUN_ID
ENV GITHUB_RUN_ID=$GITHUB_RUN_ID
ARG GITHUB_RUN_NUMBER
ENV GITHUB_RUN_NUMBER=$GITHUB_RUN_NUMBER

EXPOSE 8080
EXPOSE 8081
ARG WORKLOAD=CasCap.App.Server
ENV WORKLOAD=$WORKLOAD
ENTRYPOINT ["sh", "-c", "dotnet ${WORKLOAD}.dll"]
