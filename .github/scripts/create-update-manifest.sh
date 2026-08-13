#!/usr/bin/env bash
# Erzeugt das Release-Manifest (update.json) fuer msTools.Updater.
#
# Aufruf: create-update-manifest.sh <version> <repository (owner/name)> [ausgabedatei]
#
# Erwartet die Release-Archive release-windows-<version>.zip und release-linux-<version>.zip
# im aktuellen Arbeitsverzeichnis und schreibt daraus Groesse und SHA256 in das Manifest.
set -euo pipefail

VERSION="${1:?Version fehlt}"
REPOSITORY="${2:?Repository (owner/name) fehlt}"
OUTPUT="${3:-update.json}"

DOWNLOAD_BASE="https://github.com/${REPOSITORY}/releases/download/v${VERSION}"
PUBLISHED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

asset_json() {
  local platform="$1"
  local runtime_identifier="$2"
  local asset_name="release-${platform}-${VERSION}.zip"

  if [ ! -f "$asset_name" ]; then
    echo "Release-Archiv nicht gefunden: $asset_name" >&2
    exit 1
  fi

  local sha256 size
  sha256="$(sha256sum "$asset_name" | cut -d' ' -f1)"
  size="$(stat -c%s "$asset_name")"

  cat <<JSON
    {
      "platform": "${platform}",
      "runtimeIdentifier": "${runtime_identifier}",
      "assetName": "${asset_name}",
      "assetUrl": "${DOWNLOAD_BASE}/${asset_name}",
      "sha256": "${sha256}",
      "sizeBytes": ${size}
    }
JSON
}

{
  echo "{"
  echo "  \"version\": \"${VERSION}\","
  echo "  \"publishedAt\": \"${PUBLISHED_AT}\","
  echo "  \"assets\": ["
  asset_json "windows" "win-x64"
  echo "    ,"
  asset_json "linux" "linux-x64"
  echo "  ]"
  echo "}"
} > "$OUTPUT"

echo "Release-Manifest geschrieben: $OUTPUT"
cat "$OUTPUT"
