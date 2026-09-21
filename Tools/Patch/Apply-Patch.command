#!/bin/bash
set -euo pipefail

PATCH_DIR="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$(cd "$PATCH_DIR/.." && pwd)"
MANIFEST="$PATCH_DIR/patch-manifest.tsv"
PAYLOAD="$PATCH_DIR/payload"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP="$GAME_DIR/.ntl-patch-backup-$STAMP"
XDELTA="$PATCH_DIR/tools/xdelta3"

sha256_file() { shasum -a 256 "$1" | awk '{print tolower($1)}'; }
target_path() {
  case "$1" in
    /*|../*|*/../*) echo "Invalid patch path: $1" >&2; exit 1 ;;
  esac
  printf '%s/%s' "$GAME_DIR" "$1"
}

if [ ! -f "$MANIFEST" ]; then
  echo "Patch manifest is missing." >&2
  exit 1
fi

# Validate everything before changing the installation.
while IFS=$'\t' read -r kind rel base target mode payload_rel payload_sha; do
  [ -z "$kind" ] && continue
  dst="$(target_path "$rel")"
  if [ "$kind" = "file" ]; then
    src="$PAYLOAD/$payload_rel"
    [ -f "$src" ] && [ "$(sha256_file "$src")" = "$payload_sha" ] || { echo "Damaged payload: $rel" >&2; exit 1; }
    if [ "$mode" = "xdelta" ] && [ ! -f "$XDELTA" ]; then echo "xdelta3 decoder is missing." >&2; exit 1; fi
    if [ -f "$dst" ]; then
      current="$(sha256_file "$dst")"
      if [ "$current" != "$target" ] && { [ "$base" = "-" ] || [ "$current" != "$base" ]; }; then
        echo "Installed file does not match: $rel" >&2; exit 1
      fi
    elif [ "$base" != "-" ]; then
      echo "Installed file is missing: $rel" >&2; exit 1
    fi
  elif [ -f "$dst" ] && [ "$(sha256_file "$dst")" != "$base" ]; then
    echo "Delete target does not match: $rel" >&2; exit 1
  fi
done < "$MANIFEST"

mkdir -p "$BACKUP"
if [ -f "$XDELTA" ]; then chmod +x "$XDELTA" 2>/dev/null || true; fi
while IFS=$'\t' read -r kind rel base target mode payload_rel payload_sha; do
  [ -z "$kind" ] && continue
  dst="$(target_path "$rel")"
  if [ "$kind" = "file" ]; then
    src="$PAYLOAD/$payload_rel"
    if [ -f "$dst" ] && [ "$(sha256_file "$dst")" = "$target" ]; then continue; fi
    if [ -f "$dst" ]; then mkdir -p "$BACKUP/$(dirname "$rel")"; cp -p "$dst" "$BACKUP/$rel"; fi
    mkdir -p "$(dirname "$dst")"
    if [ "$mode" = "xdelta" ]; then
      decoded="$dst.ntl-patch-new"
      "$XDELTA" -f -d -s "$dst" "$src" "$decoded"
      [ "$(sha256_file "$decoded")" = "$target" ] || { rm -f "$decoded"; echo "Decoded file verification failed: $rel" >&2; exit 1; }
      mv -f "$decoded" "$dst"
    else
      cp -p "$src" "$dst"
    fi
  elif [ -f "$dst" ]; then
    mkdir -p "$BACKUP/$(dirname "$rel")"; cp -p "$dst" "$BACKUP/$rel"; rm -f "$dst"
  fi
done < "$MANIFEST"

while IFS=$'\t' read -r kind rel base target mode payload_rel payload_sha; do
  [ "$kind" = "file" ] || continue
  dst="$(target_path "$rel")"
  [ -f "$dst" ] && [ "$(sha256_file "$dst")" = "$target" ] || { echo "Post-patch verification failed: $rel" >&2; exit 1; }
done < "$MANIFEST"

echo "Patch complete. Backup: $BACKUP"
