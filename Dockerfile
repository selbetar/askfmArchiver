# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=5.0


FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} as builder
WORKDIR /app
COPY . .

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN dotnet publish --configuration Release --output="/askfmArchiver" --runtime linux-x64 "-p:DebugSymbols=false;DebugType=none" ./askfmArchiver/askfmArchiver.csproj


FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-alpine as app

COPY --from=builder /askfmArchiver /askfmArchiver

VOLUME [ "/app/data" ]
ENTRYPOINT ["dotnet", "/askfmArchiver/askfmArchiver.dll", "--out", "/app/data/out", "--config", "/app/data/config"]