"""VPS build-time HTTPS preflight. Keep system CA and proxy verification."""
import sys
import time
import urllib.request

for url in (
    "https://api.nuget.org/v3/index.json",
    "https://api.nuget.org/v3-flatcontainer/microsoft.windowsdesktop.app.ref/index.json",
):
    started = time.monotonic()
    print(f"[network] Checking {url}", flush=True)
    try:
        with urllib.request.urlopen(url, timeout=20) as response:
            response.read(1024)
            print(f"[network] HTTP {response.status} in {time.monotonic()-started:.1f}s", flush=True)
    except Exception as error:
        # Do not dump exception URLs/headers, which may contain proxy credentials.
        print(f"[network] HTTPS check failed ({type(error).__name__}). Check VPS/container DNS, HTTPS egress, proxy and CA trust. CPU parallelism cannot fix this.", file=sys.stderr, flush=True)
        sys.exit(1)
