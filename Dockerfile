# syntax=docker/dockerfile:1
# Run ONLY on the VPS. Independent stages are scheduled in parallel by BuildKit.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS sources
RUN apt-get update && apt-get install -y --no-install-recommends g++-mingw-w64-x86-64-posix python3 msitools zip ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /src
# Cache restore independently of application source changes.
COPY desktop/Directory.Build.props desktop/Directory.Build.props
COPY desktop/Shared/Shared.csproj desktop/Shared/Shared.csproj
COPY desktop/Server/Server.csproj desktop/Server/Server.csproj
COPY desktop/Client/Client.csproj desktop/Client/Client.csproj
COPY scripts/check-network.py scripts/check-network.py
ENV NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT=2 NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS=1000
RUN --mount=type=cache,id=remoteadmin-nuget,target=/root/.nuget/packages \
    python3 -u scripts/check-network.py \
 && echo "[restore start] $(date -u +%FT%TZ)" \
 && dotnet restore desktop/Server/Server.csproj -r win-x64 -p:EnableWindowsTargeting=true -p:SelfContained=true --verbosity normal \
 && dotnet restore desktop/Client/Client.csproj -r win-x64 -p:EnableWindowsTargeting=true -p:SelfContained=true --verbosity normal \
 && echo "[restore done] $(date -u +%FT%TZ)"
COPY desktop/ desktop/
COPY native/ native/

FROM sources AS server
ARG BUILD_JOBS=1
RUN --mount=type=cache,id=remoteadmin-nuget,target=/root/.nuget/packages \
    mkdir -p /out/RemoteAdminServer \
 && x86_64-w64-mingw32-g++-posix -std=c++20 -O2 -shared native/capture.cpp -o /out/RemoteAdminServer/remoteadmin_capture.dll -ld3d11 -ldxgi -lole32 -static-libgcc -static-libstdc++ -Wl,--no-insert-timestamp \
 && dotnet publish desktop/Server/Server.csproj --no-restore -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -maxcpucount:"$(( (BUILD_JOBS + 1) / 2 ))" -o /out/RemoteAdminServer

FROM sources AS client
ARG BUILD_JOBS=1
RUN --mount=type=cache,id=remoteadmin-nuget,target=/root/.nuget/packages \
    dotnet publish desktop/Client/Client.csproj --no-restore -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -maxcpucount:"$(( (BUILD_JOBS + 1) / 2 ))" -o /out/RemoteAdminClient

FROM sources AS package
COPY scripts/package.py scripts/package.py
COPY README.md /documentation/README.md
COPY docs/ /documentation/docs/
COPY --from=server /out/RemoteAdminServer/ /out/RemoteAdminServer/
COPY --from=client /out/RemoteAdminClient/ /out/RemoteAdminClient/
RUN python3 -u scripts/package.py /out /documentation

FROM scratch AS artifacts
COPY --from=package /artifacts/ /
