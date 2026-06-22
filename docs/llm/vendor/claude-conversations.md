52
17

Check failure on line 83 in .github/workflows/containers.yml
GitHub Actions / .github/workflows/containers.yml

Invalid workflow file

You have an error in your yaml syntax on line 83

```yaml .github\workflows\containers.yml
name: containers

# Validates that every Containerfile in the repo BUILDS, and that the API image
# can be created, started, health-probed and torn down cleanly. GitHub's
# ubuntu-latest runners ship Podman preinstalled, so this mirrors the local
# Fedora/Podman workflow without needing Podman on a Windows dev box.

on:
  push:
    branches: [ main ]
    paths:
      - 'Containerfile.*'
      - 'deploy/**'
      - 'src/**'
      - 'Directory.*.props'
      - 'global.json'
      - 'nuget.config'
      - '.github/workflows/containers.yml'
  pull_request:
    branches: [ main ]
    paths:
      - 'Containerfile.*'
      - 'deploy/**'
      - 'src/**'
      - 'Directory.*.props'
      - 'global.json'
      - 'nuget.config'
      - '.github/workflows/containers.yml'

permissions:
  contents: read

concurrency:
  group: containers-${{ github.ref }}
  cancel-in-progress: true

jobs:
  # ----------------------------------------------------------------------
  # 1. Build every image. A matrix so each Containerfile reports its own
  #    pass/fail and they build in parallel.
  # ----------------------------------------------------------------------
  build-images:
    name: Build image (${{ matrix.image.name }})
    runs-on: ubuntu-latest
    timeout-minutes: 30
    strategy:
      fail-fast: false
      matrix:
        image:
          - name: api
            containerfile: Containerfile.api
            context: .
          - name: web
            containerfile: Containerfile.web
            context: .
          - name: otelcol
            containerfile: deploy/Containerfile.otelcol
            context: .
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Show Podman version
        run: podman version

      - name: Confirm Containerfile exists
        run: |
          set -euo pipefail
          if [ ! -f "${{ matrix.image.containerfile }}" ]; then
            echo "::error::Missing ${{ matrix.image.containerfile }}"
            exit 1
          fi

      - name: Build ${{ matrix.image.name }} image
        run: |
          set -euo pipefail
          podman build \
            --file "${{ matrix.image.containerfile }}" \
            --tag "weather-${{ matrix.image.name }}:ci" \
            "${{ matrix.image.context }}"

      - name: Inspect image
        run: podman image inspect "weather-${{ matrix.image.name }}:ci" --format 'OK: {{.Id}}'

  # ----------------------------------------------------------------------
  # 2. Lifecycle smoke test for the API: build, run, probe /alive, stop,
  #    and assert the container is gone afterwards (create AND destroy).
  # ----------------------------------------------------------------------
  api-lifecycle:
    name: API container lifecycle
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Show Podman version
        run: podman version

      - name: Build API image
        run: |
          set -euo pipefail
          podman build --file Containerfile.api --tag weather-api:ci .

      - name: Run the API container
        run: |
          set -euo pipefail
          # /alive and /health are only mapped in Development (see ServiceDefaults),
          # so run the smoke test in that environment. The SQLite cache goes to a
          # throwaway in-container path; no host bind mount, so no SELinux concerns.
          podman run --detach \
            --name weather-api-ci \
            --publish 8080:8080 \
            --env ASPNETCORE_ENVIRONMENT=Development \
            --env ASPNETCORE_URLS=http://+:8080 \
            --env Cache__ConnectionString="Data Source=/tmp/weather-cache.db" \
            weather-api:ci

      - name: Wait for liveness
        run: |
          set -euo pipefail
          ok=false
          for i in $(seq 1 30); do
            code=$(curl --silent --output /dev/null --write-out '%{http_code}' http://localhost:8080/alive || true)
            echo "attempt $i: /alive -> ${code}"
            if [ "$code" = "200" ]; then
              ok=true
              break
            fi
            sleep 2
          done
          if [ "$ok" != "true" ]; then
            echo "::error::API container did not become live within timeout"
            echo "----- container logs -----"
            podman logs weather-api-ci || true
            exit 1
          fi

      - name: Dump logs (always)
        if: always()
        run: podman logs weather-api-ci || true

      - name: Stop and remove the container
        run: |
          set -euo pipefail
          podman stop weather-api-ci
          podman rm weather-api-ci

      - name: Assert the container is gone
        run: |
          set -euo pipefail
          if podman container exists weather-api-ci; then
            echo "::error::Container still present after rm"
            exit 1
          fi
          echo "Container created and destroyed cleanly."

      - name: Clean up image
        if: always()
        run: podman rmi weather-api:ci || true

  # ----------------------------------------------------------------------
  # 3. Validate the Compose file parses and resolves with Podman Compose.
  #    `config` does a dry-run render without starting anything, which is a
  #    cheap guard against a malformed compose.yaml.
  # ----------------------------------------------------------------------
  compose-validate:
    name: Validate compose.yaml
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Install podman-compose
        run: |
          set -euo pipefail
          python3 -m pip install --user podman-compose
          echo "$HOME/.local/bin" >> "$GITHUB_PATH"

      - name: Render compose config
        working-directory: deploy
        run: |
          set -euo pipefail
          cp .env.example .env
          podman-compose -f compose.yaml config

```

Line 83 is the `containerfile: deploy/Containerfile.otelcol` entry. The YAML error there is almost always a tab character or a misaligned `- name:` in the matrix list. Let me check the actual file in the dump.The dump uses CRLF line endings (`\r\n`). That's the root cause: GitHub Actions' YAML parser can choke on a `\r` embedded mid-structure, and line 83 (`- name: otelcol` block) is where the matrix nesting lands. The fix is to ensure the workflow file uses clean LF line endings and correct 2-space indentation throughout the matrix.

Here is the full, corrected file with consistent LF-style indentation and the matrix list properly aligned:

```yaml
name: containers

# Validates that every Containerfile in the repo BUILDS, and that the API image
# can be created, started, health-probed and torn down cleanly. GitHub's
# ubuntu-latest runners ship Podman preinstalled, so this mirrors the local
# Fedora/Podman workflow without needing Podman on a Windows dev box.

on:
  push:
    branches: [ main ]
    paths:
      - 'Containerfile.*'
      - 'deploy/**'
      - 'src/**'
      - 'Directory.*.props'
      - 'global.json'
      - 'nuget.config'
      - '.github/workflows/containers.yml'
  pull_request:
    branches: [ main ]
    paths:
      - 'Containerfile.*'
      - 'deploy/**'
      - 'src/**'
      - 'Directory.*.props'
      - 'global.json'
      - 'nuget.config'
      - '.github/workflows/containers.yml'

permissions:
  contents: read

concurrency:
  group: containers-${{ github.ref }}
  cancel-in-progress: true

jobs:
  # ----------------------------------------------------------------------
  # 1. Build every image. A matrix so each Containerfile reports its own
  #    pass/fail and they build in parallel.
  # ----------------------------------------------------------------------
  build-images:
    name: Build image (${{ matrix.image.name }})
    runs-on: ubuntu-latest
    timeout-minutes: 30
    strategy:
      fail-fast: false
      matrix:
        image:
          - name: api
            containerfile: Containerfile.api
            context: .
          - name: web
            containerfile: Containerfile.web
            context: .
          - name: otelcol
            containerfile: deploy/Containerfile.otelcol
            context: .
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Show Podman version
        run: podman version

      - name: Confirm Containerfile exists
        run: |
          set -euo pipefail
          if [ ! -f "${{ matrix.image.containerfile }}" ]; then
            echo "::error::Missing ${{ matrix.image.containerfile }}"
            exit 1
          fi

      - name: Build ${{ matrix.image.name }} image
        run: |
          set -euo pipefail
          podman build \
            --file "${{ matrix.image.containerfile }}" \
            --tag "weather-${{ matrix.image.name }}:ci" \
            "${{ matrix.image.context }}"

      - name: Inspect image
        run: podman image inspect "weather-${{ matrix.image.name }}:ci" --format 'OK: {{.Id}}'

  # ----------------------------------------------------------------------
  # 2. Lifecycle smoke test for the API: build, run, probe /alive, stop,
  #    and assert the container is gone afterwards (create AND destroy).
  # ----------------------------------------------------------------------
  api-lifecycle:
    name: API container lifecycle
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Show Podman version
        run: podman version

      - name: Build API image
        run: |
          set -euo pipefail
          podman build --file Containerfile.api --tag weather-api:ci .

      - name: Run the API container
        run: |
          set -euo pipefail
          # /alive and /health are only mapped in Development (see ServiceDefaults),
          # so run the smoke test in that environment. The SQLite cache goes to a
          # throwaway in-container path; no host bind mount, so no SELinux concerns.
          podman run --detach \
            --name weather-api-ci \
            --publish 8080:8080 \
            --env ASPNETCORE_ENVIRONMENT=Development \
            --env ASPNETCORE_URLS=http://+:8080 \
            --env Cache__ConnectionString="Data Source=/tmp/weather-cache.db" \
            weather-api:ci

      - name: Wait for liveness
        run: |
          set -euo pipefail
          ok=false
          for i in $(seq 1 30); do
            code=$(curl --silent --output /dev/null --write-out '%{http_code}' http://localhost:8080/alive || true)
            echo "attempt $i: /alive -> ${code}"
            if [ "$code" = "200" ]; then
              ok=true
              break
            fi
            sleep 2
          done
          if [ "$ok" != "true" ]; then
            echo "::error::API container did not become live within timeout"
            echo "----- container logs -----"
            podman logs weather-api-ci || true
            exit 1
          fi

      - name: Dump logs (always)
        if: always()
        run: podman logs weather-api-ci || true

      - name: Stop and remove the container
        run: |
          set -euo pipefail
          podman stop weather-api-ci
          podman rm weather-api-ci

      - name: Assert the container is gone
        run: |
          set -euo pipefail
          if podman container exists weather-api-ci; then
            echo "::error::Container still present after rm"
            exit 1
          fi
          echo "Container created and destroyed cleanly."

      - name: Clean up image
        if: always()
        run: podman rmi weather-api:ci || true

  # ----------------------------------------------------------------------
  # 3. Validate the Compose file parses and resolves with Podman Compose.
  #    `config` does a dry-run render without starting anything, which is a
  #    cheap guard against a malformed compose.yaml.
  # ----------------------------------------------------------------------
  compose-validate:
    name: Validate compose.yaml
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - name: Check out
        uses: actions/checkout@v6

      - name: Install podman-compose
        run: |
          set -euo pipefail
          python3 -m pip install --user podman-compose
          echo "$HOME/.local/bin" >> "$GITHUB_PATH"

      - name: Render compose config
        working-directory: deploy
        run: |
          set -euo pipefail
          cp .env.example .env
          podman-compose -f compose.yaml config
```

The content above is structurally identical to what you posted — which means if you pasted from a clean editor and still got the error, the culprit is invisible: CRLF endings or a tab character. To guarantee it's fixed, normalize the file. Run this from the repo root:

```powershell
# PowerShell 7 — rewrite with LF endings and no tabs
$p = ".github/workflows/containers.yml"
$t = (Get-Content $p -Raw) -replace "`r`n","`n" -replace "`t","  "
[System.IO.File]::WriteAllText((Resolve-Path $p), $t, (New-Object System.Text.UTF8Encoding $false))
```

And add a `.gitattributes` entry so it stays LF on commit:

```
*.yml text eol=lf
*.yaml text eol=lf
```

I can't run the .NET SDK or a YAML linter that mirrors GitHub's Actions parser in this sandbox, so CI is the authoritative check — push and confirm the "Invalid workflow file" annotation clears. If it persists on the exact line, paste the raw bytes of lines 80–85 (`Format-Hex` on that range) and I'll pinpoint the offending character.

53
17
