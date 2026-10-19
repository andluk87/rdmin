#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
# Default: artifacts in the repository root. Use OUTPUT_DIR=/root if required.
artifact_dir="${OUTPUT_DIR:-$project_dir}"
mkdir -p -- "$artifact_dir"
command -v docker >/dev/null || { echo 'Docker is required on the VPS.' >&2; exit 1; }
build_jobs="${BUILD_JOBS:-$(nproc)}"
[[ "$build_jobs" =~ ^[1-9][0-9]*$ ]] || { echo 'BUILD_JOBS must be a positive integer.' >&2; exit 1; }
echo "Building with $build_jobs available CPU threads (BuildKit parallel stages)."
DOCKER_BUILDKIT=1 docker build --build-arg "BUILD_JOBS=$build_jobs" --target artifacts --output "type=local,dest=$artifact_dir" "$project_dir"
(cd -- "$artifact_dir" && sha256sum -c SHA256SUMS)
echo "Windows artifacts: $artifact_dir"
