# Run ONLY on the VPS. Independent stages are scheduled in parallel by BuildKit.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS sources
RUN apt-get update && apt-get install -y --no-install-recommends g++-mingw-w64-x86-64-posix python3 msitools zip ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY desktop/ desktop/
COPY native/ native/

FROM sources AS server
ARG BUILD_JOBS=1
RUN mkdir -p /out/RemoteAdminServer \
 && x86_64-w64-mingw32-g++-posix -std=c++20 -O2 -shared native/capture.cpp -o /out/RemoteAdminServer/remoteadmin_capture.dll -ld3d11 -ldxgi -lole32 -static-libgcc -static-libstdc++ -Wl,--no-insert-timestamp \
 && dotnet publish desktop/Server/Server.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -maxcpucount:"$(( (BUILD_JOBS + 1) / 2 ))" -o /out/RemoteAdminServer

FROM sources AS client
ARG BUILD_JOBS=1
RUN dotnet publish desktop/Client/Client.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -maxcpucount:"$(( (BUILD_JOBS + 1) / 2 ))" -o /out/RemoteAdminClient

FROM sources AS package
COPY scripts/package.py scripts/package.py
COPY README.md /documentation/README.md
COPY docs/ /documentation/docs/
COPY --from=server /out/RemoteAdminServer/ /out/RemoteAdminServer/
COPY --from=client /out/RemoteAdminClient/ /out/RemoteAdminClient/
RUN python3 scripts/package.py /out /documentation

FROM scratch AS artifacts
COPY --from=package /artifacts/ /
