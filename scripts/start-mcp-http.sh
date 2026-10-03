#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
host="${MCP_HTTP_HOST:-0.0.0.0}"
port="${MCP_HTTP_PORT:-5080}"

export Mcp__Transport=http
export ASPNETCORE_URLS="http://${host}:${port}"

if [[ -z "${Mcp__ApiKey:-}" ]]; then
  printf '%s\n' 'Set Mcp__ApiKey to a secret before starting the MCP HTTP server.' >&2
  exit 1
fi

exec dotnet run \
  --project "${repo_root}/src/FileRepo.Mcp/FileRepo.Mcp.csproj" \
  "$@"
