# syntax=docker/dockerfile:1
# check=skip=CopyIgnoredFile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["Directory.Build.props", "Directory.Packages.props", "appsettings.json", "./"]

ARG WORKLOAD=CasCap.App.Server
ARG CONFIGURATION=Release
COPY ["src/$WORKLOAD/$WORKLOAD.csproj", "src/$WORKLOAD/"]
COPY ["src/CasCap.App/CasCap.App.csproj", "src/CasCap.App/"]
COPY ["src/CasCap.Api.Buderus/CasCap.Api.Buderus.csproj", "src/CasCap.Api.Buderus/"]
COPY ["src/CasCap.Api.Buderus.Sinks/CasCap.Api.Buderus.Sinks.csproj", "src/CasCap.Api.Buderus.Sinks/"]
COPY ["src/CasCap.Api.DDns/CasCap.Api.DDns.csproj", "src/CasCap.Api.DDns/"]
COPY ["src/CasCap.Api.DoorBird/CasCap.Api.DoorBird.csproj", "src/CasCap.Api.DoorBird/"]
COPY ["src/CasCap.Api.DoorBird.Sinks/CasCap.Api.DoorBird.Sinks.csproj", "src/CasCap.Api.DoorBird.Sinks/"]
COPY ["src/CasCap.Api.Fronius/CasCap.Api.Fronius.csproj", "src/CasCap.Api.Fronius/"]
COPY ["src/CasCap.Api.Fronius.Sinks/CasCap.Api.Fronius.Sinks.csproj", "src/CasCap.Api.Fronius.Sinks/"]
COPY ["src/CasCap.Api.Knx/CasCap.Api.Knx.csproj", "src/CasCap.Api.Knx/"]
COPY ["src/CasCap.Api.Knx.Sinks/CasCap.Api.Knx.Sinks.csproj", "src/CasCap.Api.Knx.Sinks/"]
COPY ["src/CasCap.Api.Miele/CasCap.Api.Miele.csproj", "src/CasCap.Api.Miele/"]
COPY ["src/CasCap.Api.Miele.Sinks/CasCap.Api.Miele.Sinks.csproj", "src/CasCap.Api.Miele.Sinks/"]
COPY ["src/CasCap.Api.EdgeHardware/CasCap.Api.EdgeHardware.csproj", "src/CasCap.Api.EdgeHardware/"]
COPY ["src/CasCap.Api.EdgeHardware.Sinks/CasCap.Api.EdgeHardware.Sinks.csproj", "src/CasCap.Api.EdgeHardware.Sinks/"]
COPY ["src/CasCap.Api.SignalCli/CasCap.Api.SignalCli.csproj", "src/CasCap.Api.SignalCli/"]
COPY ["src/CasCap.Api.Sicce/CasCap.Api.Sicce.csproj", "src/CasCap.Api.Sicce/"]
COPY ["src/CasCap.Api.Sicce.Sinks/CasCap.Api.Sicce.Sinks.csproj", "src/CasCap.Api.Sicce.Sinks/"]
COPY ["src/CasCap.Api.Shelly/CasCap.Api.Shelly.csproj", "src/CasCap.Api.Shelly/"]
COPY ["src/CasCap.Api.Shelly.Sinks/CasCap.Api.Shelly.Sinks.csproj", "src/CasCap.Api.Shelly.Sinks/"]
COPY ["src/CasCap.Api.Ubiquiti/CasCap.Api.Ubiquiti.csproj", "src/CasCap.Api.Ubiquiti/"]
COPY ["src/CasCap.Api.Ubiquiti.Sinks/CasCap.Api.Ubiquiti.Sinks.csproj", "src/CasCap.Api.Ubiquiti.Sinks/"]
COPY ["src/CasCap.Api.Wiz/CasCap.Api.Wiz.csproj", "src/CasCap.Api.Wiz/"]
COPY ["src/CasCap.Api.Wiz.Sinks/CasCap.Api.Wiz.Sinks.csproj", "src/CasCap.Api.Wiz.Sinks/"]
COPY ["src/CasCap.SmartHaus/CasCap.SmartHaus.csproj", "src/CasCap.SmartHaus/"]
ARG TARGETPLATFORM
RUN --mount=type=cache,id=nuget-${TARGETPLATFORM},target=/root/.nuget/packages \
    dotnet restore "src/$WORKLOAD/$WORKLOAD.csproj"
COPY . .
RUN --mount=type=cache,id=nuget-${TARGETPLATFORM},target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
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
