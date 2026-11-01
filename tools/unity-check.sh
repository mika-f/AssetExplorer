#!/usr/bin/env bash
# Copies the project to a scratch directory, compiles it and runs the EditMode tests in Unity batch mode,
# then copies any .meta files Unity generated for new assets back into the repository.
#
#   UNITY=/path/to/Unity DEST=/path/to/copy tools/unity-check.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity}"
DEST="${DEST:-${TMPDIR:-/tmp}/petrichor-unity-check}"

mkdir -p "$DEST"
# Library/ is kept between runs so later imports are incremental.
rsync -a --delete --filter 'P Library/' --filter 'P Logs/' --filter 'P UserSettings/' \
  --exclude Library --exclude Temp --exclude Logs --exclude UserSettings --exclude .git --exclude .superpowers --exclude tools \
  --exclude results.xml --exclude editor.log \
  "$ROOT/" "$DEST/"
rm -f "$DEST/results.xml"

set +e
"$UNITY" -batchmode -nographics -projectPath "$DEST" -runTests -testPlatform EditMode \
  -testResults "$DEST/results.xml" -logFile "$DEST/editor.log"
status=$?
set -e

if grep -qE "error CS[0-9]+" "$DEST/editor.log"; then
  echo "COMPILE ERRORS:"
  grep -E "error CS[0-9]+" "$DEST/editor.log" | sort -u
  exit 1
fi

if [ ! -f "$DEST/results.xml" ]; then
  echo "No test results (Unity exit $status). See $DEST/editor.log"
  tail -40 "$DEST/editor.log"
  exit 1
fi

summary=$(grep -o '<test-run [^>]*>' "$DEST/results.xml" | head -1)
passed=$(echo "$summary" | sed -E 's/.* passed="([0-9]+)".*/\1/')
failed=$(echo "$summary" | sed -E 's/.* failed="([0-9]+)".*/\1/')

# Bring back .meta files for new assets; existing ones are never overwritten.
rsync -a --ignore-existing --include '*/' --include '*.meta' --exclude '*' \
  "$DEST/Assets/NatsunekoLaboratory/" "$ROOT/Assets/NatsunekoLaboratory/"

echo "RESULT passed=$passed failed=$failed"
if [ "$failed" != "0" ]; then
  grep -B2 -A8 'result="Failed"' "$DEST/results.xml" | grep -E '<test-case|<message' | head -40
  exit 1
fi
