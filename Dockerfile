# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=6.0


FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} as files
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
COPY . .

FROM files as builder
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN dotnet publish --configuration Release --output="/askfmArchiver-out" --runtime linux-x64 --self-contained "-p:DebugSymbols=false;DebugType=none" ./askfmArchiver/askfmArchiver.csproj


FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} as app
WORKDIR /data/

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=builder /askfmArchiver-out /askfmArchiver

VOLUME [ "/data" ]
ENTRYPOINT ["/askfmArchiver/./askfmArchiver"]
