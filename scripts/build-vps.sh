#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
# Default: artifacts in the repository root. Use OUTPUT_DIR=/root if required.
artifact_dir="${OUTPUT_DIR:-$project_dir}"
mkdir -p -- "$artifact_dir"
command -v docker >/dev/null || { echo 'Docker is required on the VPS.' >&2; exit 1; }
DOCKER_BUILDKIT=1 docker build --target artifacts --output "type=local,dest=$artifact_dir" "$project_dir"
(cd -- "$artifact_dir" && sha256sum -c SHA256SUMS)
echo "Windows artifacts: $artifact_dir"
