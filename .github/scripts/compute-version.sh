#!/usr/bin/env bash
# Berechnet die naechste Version anhand semantischer Regeln (Conventional Commits).
#
# Basis ist der letzte stabile Tag (vX.Y.Z ohne Suffix). Alle Commits zwischen
# diesem Tag und HEAD bestimmen den Bump:
#   * "BREAKING CHANGE" / "<type>!:"  -> major
#   * "feat:" / "feat(scope):"        -> minor
#   * alles andere                    -> patch
#
# Der RC-Zaehler wird pro Zielversion gefuehrt: er ergibt sich aus dem hoechsten
# bereits vorhandenen RC-Tag dieser Version + 1 und beginnt damit fuer jede neue
# Version (z. B. nach einem Release und dem Back-Merge) wieder bei 1.
#
# Ausgabe (nach $GITHUB_OUTPUT, falls gesetzt, sonst stdout):
#   base_version   letzte stabile Version (ohne "v")
#   bump           major|minor|patch
#   next_version   naechste Version (ohne "v")
#   rc_number      naechster RC-Zaehler fuer next_version (beginnt bei 1)
#   rc_version     next_version mit RC-Suffix, z. B. 1.2.0-RC.1
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

# RC-Zaehler pro Version: hoechster vorhandener RC-Tag dieser Version + 1.
LAST_RC=$(git tag -l "v${NEXT_VERSION}-RC.*" \
  | sed -n "s/^v${NEXT_VERSION}-RC\.\([0-9]\+\)$/\1/p" \
  | sort -n | tail -n 1)
RC_NUMBER=$(( ${LAST_RC:-0} + 1 ))
RC_VERSION="${NEXT_VERSION}-RC.${RC_NUMBER}"

echo "Letzter stabiler Tag: ${LATEST_STABLE_TAG:-<keiner>}"
echo "Basis-Version: $BASE_VERSION"
echo "Bump: $BUMP"
echo "Naechste Version: $NEXT_VERSION"
echo "RC-Version: $RC_VERSION"

OUT=${GITHUB_OUTPUT:-/dev/stdout}
{
  echo "base_version=$BASE_VERSION"
  echo "bump=$BUMP"
  echo "next_version=$NEXT_VERSION"
  echo "rc_number=$RC_NUMBER"
  echo "rc_version=$RC_VERSION"
} >>"$OUT"
