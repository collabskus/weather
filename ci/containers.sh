#!/usr/bin/env bash
#
# ci/containers.sh — every bit of container CI logic, in one place.
#
# WHY THIS SCRIPT EXISTS
# ----------------------
# The container workflow used to inline all of this shell directly inside
# `run:` blocks in .github/workflows/containers.yml. One of those lines was:
#
#     run: podman image inspect "weather-...:ci" --format 'OK: {{.Id}}'
#
# That entire value is an *unquoted* YAML scalar, so the quote characters in it
# are literal text — not delimiters. The parser therefore saw the ": " inside
# 'OK: {{.Id}}' as a key/value separator, the file stopped being valid YAML,
# GitHub could not even read `name:`, scheduled ZERO jobs, and marked every run
# as a failure. (It was never a line-ending problem.)
#
# Moving the shell out of YAML and into this file deletes that whole class of
# bug: the workflow now only contains trivial `bash ci/containers.sh <cmd>`
# lines, and the real logic lives here where bash — not a YAML parser — reads
# it. Bonus: you can run every CI step locally, byte-for-byte the same as CI.
#
# USAGE
# -----
#   ci/containers.sh build api          # build + inspect a single image
#   ci/containers.sh build web
#   ci/containers.sh build otelcol
#   ci/containers.sh api-lifecycle      # build, run, probe /alive, then destroy
#   ci/containers.sh compose-validate   # render deploy/compose.yaml (dry run)
#
# ROOTFUL vs ROOTLESS PODMAN
# --------------------------
# GitHub's `ubuntu-latest` is Ubuntu 24.04, which ships
# kernel.apparmor_restrict_unprivileged_userns=1 by default. That blocks the
# user-namespace setup rootless podman needs, so `podman build` / `podman run`
# fail on the runner with newuidmap/userns errors. Running podman under sudo
# (rootful) sidesteps it entirely — the runner user has passwordless sudo, and
# rootful port publishing also makes the /alive probe reliable. Locally
# (e.g. your Fedora box) we stay rootless, so nothing about your normal
# workflow changes. Force either mode with PODMAN_ROOTFUL=1 or PODMAN_ROOTFUL=0.
#
set -euo pipefail

# --- locate the repo root so the script works from any directory ------------
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# --- tunables (all overridable from the environment) ------------------------
CI_TAG="${CI_TAG:-ci}"
API_PORT="${API_PORT:-8080}"
API_CONTAINER="${API_CONTAINER:-weather-api-ci}"
PROBE_ATTEMPTS="${PROBE_ATTEMPTS:-30}"
PROBE_INTERVAL="${PROBE_INTERVAL:-2}"

# --- pretty logging ---------------------------------------------------------
log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[error]\033[0m %s\n' "$*" >&2; exit 1; }

# --- choose rootful vs rootless podman --------------------------------------
choose_podman() {
  local rootful
  if [[ -n "${PODMAN_ROOTFUL:-}" ]]; then
    rootful="${PODMAN_ROOTFUL}"
  elif [[ "${GITHUB_ACTIONS:-}" == "true" && "$(uname -s)" == "Linux" ]]; then
    rootful="1"
  else
    rootful="0"
  fi

  if [[ "${rootful}" == "1" ]]; then
    PODMAN=(sudo podman)
  else
    PODMAN=(podman)
  fi
}
choose_podman

# Run podman with the chosen privilege level. PODMAN is an array, so quoting
# stays correct whether it expands to `podman` or `sudo podman`.
pm() { "${PODMAN[@]}" "$@"; }

# --- image name -> Containerfile mapping ------------------------------------
containerfile_for() {
  case "${1:-}" in
    api)     printf '%s\n' 'Containerfile.api' ;;
    web)     printf '%s\n' 'Containerfile.web' ;;
    otelcol) printf '%s\n' 'deploy/Containerfile.otelcol' ;;
    *) die "unknown image '${1:-}' (expected: api | web | otelcol)" ;;
  esac
}

# ============================================================================
# command: build <image>
# ============================================================================
cmd_build() {
  local name="${1:-}"
  [[ -n "${name}" ]] || die "usage: ci/containers.sh build <api|web|otelcol>"

  local file
  file="$(containerfile_for "${name}")"
  [[ -f "${file}" ]] || die "missing Containerfile: ${file}"

  log "podman version"
  pm version

  local tag="weather-${name}:${CI_TAG}"
  log "building ${tag} from ${file}"
  # --pull=always refreshes the base image (e.g. mcr.microsoft.com/dotnet/sdk:10.0)
  # on every build. The floating ':10.0' tag advances through SDK feature bands
  # over time (10.0.1xx -> 2xx -> 3xx ...). A long-lived host that pulled it months
  # ago can be stuck on an OLD local digest — even a prerelease from the RC period —
  # that no longer satisfies the version pin in global.json, so `dotnet restore`
  # dies with "A compatible .NET SDK was not found. Requested SDK version: 10.0.x".
  # Re-pulling eliminates that whole class of stale-cache failure. We use 'always'
  # rather than 'newer' on purpose: podman's *build* path historically ignores
  # --pull=newer and silently falls back to 'missing' (containers/podman#22845).
  # 'always' is reliable everywhere; when the local digest already matches the
  # registry, unchanged layers are not re-downloaded, so the cost is a cheap
  # manifest check. (Override with PODMAN build flags if you ever build offline.)
  pm build --pull=always --file "${file}" --tag "${tag}" .

  log "inspecting ${tag}"
  pm image inspect "${tag}" --format 'OK: {{.Id}}'

  log "built ${tag} successfully"
}

# ============================================================================
# command: api-lifecycle  (build -> run -> probe /alive -> stop -> rm -> gone)
# ============================================================================
cmd_api_lifecycle() {
  local tag="weather-api:${CI_TAG}"
  local name="${API_CONTAINER}"

  # Best-effort teardown on ANY exit, so we never leak a container or image.
  #
  # IMPORTANT: an EXIT trap fires *after* this function has already returned and
  # its `local` variables have gone out of scope. Referencing ${name}/${tag}
  # here would therefore expand an unset variable, and under `set -u` that is a
  # fatal error (the `|| true` can't save it — the failure happens during
  # expansion, before `||` is ever reached). So the trap reads the durable
  # script-scope config instead, which is still defined when the trap runs.
  cleanup() {
    pm rm --force "${API_CONTAINER}"   >/dev/null 2>&1 || true
    pm rmi "weather-api:${CI_TAG}"     >/dev/null 2>&1 || true
  }
  trap cleanup EXIT

  log "podman version"
  pm version

  log "building ${tag}"
  # See cmd_build for the rationale: --pull=always keeps the SDK/runtime base
  # images fresh so a stale local digest can't fail the build against the pin
  # in global.json.
  pm build --pull=always --file Containerfile.api --tag "${tag}" .

  # /alive and /health are only mapped in Development (see ServiceDefaults), so
  # the smoke test runs in that environment. The SQLite cache goes to a
  # throwaway in-container path — no host bind mount, so no SELinux concerns
  # and nothing to clean up on the host.
  log "starting ${name} (publishing :${API_PORT})"
  pm run --detach \
    --name "${name}" \
    --publish "${API_PORT}:8080" \
    --env ASPNETCORE_ENVIRONMENT=Development \
    --env ASPNETCORE_URLS='http://+:8080' \
    --env Cache__ConnectionString='Data Source=/tmp/weather-cache.db' \
    "${tag}"

  log "waiting for http://localhost:${API_PORT}/alive"
  local code=""
  local live="false"
  local attempt
  for (( attempt = 1; attempt <= PROBE_ATTEMPTS; attempt++ )); do
    code="$(curl --silent --output /dev/null --write-out '%{http_code}' \
      "http://localhost:${API_PORT}/alive" || true)"
    printf '  attempt %2d/%d: /alive -> %s\n' \
      "${attempt}" "${PROBE_ATTEMPTS}" "${code:-no-response}"
    if [[ "${code}" == "200" ]]; then
      live="true"
      break
    fi
    sleep "${PROBE_INTERVAL}"
  done

  if [[ "${live}" != "true" ]]; then
    warn "API never became live; dumping container logs:"
    pm logs "${name}" || true
    die "API container did not become live within timeout"
  fi
  log "API is live"

  log "stopping and removing ${name}"
  pm stop "${name}"
  pm rm "${name}"

  if pm container exists "${name}"; then
    die "container still present after rm"
  fi
  log "container created and destroyed cleanly"
}

# ============================================================================
# command: compose-validate  (render deploy/compose.yaml without starting it)
# ============================================================================
ensure_podman_compose() {
  if command -v podman-compose >/dev/null 2>&1; then
    COMPOSE=(podman-compose)
    return
  fi
  # Ubuntu 24.04's system Python is externally managed (PEP 668), so a bare
  # `pip install` is refused. A throwaway venv avoids that cleanly and leaves
  # no global state behind.
  log "podman-compose not found; installing into a throwaway venv"
  local venv="${TMPDIR:-/tmp}/weather-compose-venv"
  python3 -m venv "${venv}"
  "${venv}/bin/pip" install --quiet --upgrade pip
  "${venv}/bin/pip" install --quiet podman-compose
  COMPOSE=("${venv}/bin/podman-compose")
}

cmd_compose_validate() {
  ensure_podman_compose
  log "podman-compose ready"
  "${COMPOSE[@]}" --version || true

  # `config` is a dry-run render: it parses and normalizes the compose file but
  # starts nothing, so rootless podman is fine here even on Ubuntu 24.04.
  log "rendering deploy/compose.yaml"
  (
    cd deploy
    cp .env.example .env
    "${COMPOSE[@]}" -f compose.yaml config
  )
  log "deploy/compose.yaml is valid"
}

# ============================================================================
# dispatcher
# ============================================================================
main() {
  local command="${1:-}"
  shift || true
  case "${command}" in
    build)            cmd_build "$@" ;;
    api-lifecycle)    cmd_api_lifecycle "$@" ;;
    compose-validate) cmd_compose_validate "$@" ;;
    ''|-h|--help|help)
      cat <<'USAGE'
Usage: ci/containers.sh <command> [args]

Commands:
  build <api|web|otelcol>   Build the image and inspect it.
  api-lifecycle             Build the API image, run it, probe /alive, then
                            stop, remove and assert the container is gone.
  compose-validate          Render deploy/compose.yaml (dry run, starts nothing).
USAGE
      ;;
    *) die "unknown command '${command}' (try: build | api-lifecycle | compose-validate)" ;;
  esac
}

main "$@"
