# Run ONLY on the VPS. This stage cross-compiles Windows x64 binaries on Linux.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
RUN apt-get update && apt-get install -y --no-install-recommends g++-mingw-w64-x86-64-posix python3 msitools zip ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY desktop/ desktop/
COPY native/ native/
COPY scripts/package.py scripts/package.py
COPY README.md /documentation/README.md
COPY docs/ /documentation/docs/
RUN mkdir -p /out/RemoteAdminServer /out/RemoteAdminClient \
 && x86_64-w64-mingw32-g++-posix -std=c++20 -O2 -shared native/capture.cpp -o /out/RemoteAdminServer/remoteadmin_capture.dll -ld3d11 -ldxgi -lole32 -static-libgcc -static-libstdc++ -Wl,--no-insert-timestamp \
 && dotnet publish desktop/Server/Server.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o /out/RemoteAdminServer \
 && dotnet publish desktop/Client/Client.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o /out/RemoteAdminClient \
 && python3 scripts/package.py /out /documentation
FROM scratch AS artifacts
COPY --from=build /artifacts/ /
