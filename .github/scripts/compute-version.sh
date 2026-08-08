#!/usr/bin/env bash
# Berechnet die naechste Version anhand semantischer Regeln (Conventional Commits).
#
# Basis ist der letzte stabile Tag (vX.Y.Z ohne Suffix). Alle Commits zwischen
# diesem Tag und HEAD bestimmen den Bump:
#   * "BREAKING CHANGE" / "<type>!:"  -> major
#   * "feat:" / "feat(scope):"        -> minor
#   * alles andere                    -> patch
#
# Ausgabe (nach $GITHUB_OUTPUT, falls gesetzt, sonst stdout):
#   base_version   letzte stabile Version (ohne "v")
#   bump           major|minor|patch
#   next_version   naechste Version (ohne "v")
set -euo pipefail

LATEST_STABLE_TAG=$(git tag -l 'v[0-9]*.[0-9]*.[0-9]*' | grep -Ev -- '-' | sort -V | tail -n 1 || true)

if [ -z "$LATEST_STABLE_TAG" ]; then
  BASE_VERSION="0.0.0"
  RANGE=""
else
  BASE_VERSION=${LATEST_STABLE_TAG#v}
  RANGE="${LATEST_STABLE_TAG}..HEAD"
fi

if [ -n "$RANGE" ]; then
  COMMITS=$(git log --format=%B "$RANGE" || true)
else
  COMMITS=$(git log --format=%B || true)
fi

BUMP="patch"
if printf '%s' "$COMMITS" | grep -qE '^[[:space:]]*BREAKING[ -]CHANGE' \
  || printf '%s' "$COMMITS" | grep -qE '^[a-zA-Z]+(\([^)]*\))?!:'; then
  BUMP="major"
elif printf '%s' "$COMMITS" | grep -qE '^feat(\([^)]*\))?:'; then
  BUMP="minor"
fi

IFS='.' read -r MAJOR MINOR PATCH <<<"$BASE_VERSION"
MAJOR=${MAJOR:-0}; MINOR=${MINOR:-0}; PATCH=${PATCH:-0}

case "$BUMP" in
  major) MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0 ;;
  minor) MINOR=$((MINOR + 1)); PATCH=0 ;;
  patch) PATCH=$((PATCH + 1)) ;;
esac

NEXT_VERSION="${MAJOR}.${MINOR}.${PATCH}"

echo "Letzter stabiler Tag: ${LATEST_STABLE_TAG:-<keiner>}"
echo "Basis-Version: $BASE_VERSION"
echo "Bump: $BUMP"
echo "Naechste Version: $NEXT_VERSION"

OUT=${GITHUB_OUTPUT:-/dev/stdout}
{
  echo "base_version=$BASE_VERSION"
  echo "bump=$BUMP"
  echo "next_version=$NEXT_VERSION"
} >>"$OUT"
