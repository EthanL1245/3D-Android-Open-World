#!/bin/bash
set -euo pipefail
export GIT_TERMINAL_PROMPT=0
base=/opt/fishing-server
export npm_config_cache="$base/npm-cache"
repo="$base/source"
if [[ ! -d "$repo/.git" ]]; then
  git clone --depth=1 --filter=blob:none --single-branch --branch main --no-checkout \
    https://github.com/EthanL1245/3D-Android-Open-World.git "$repo" >&2
  git -C "$repo" sparse-checkout set --no-cone /Server/runtime/ >&2
fi
git -C "$repo" fetch --depth=1 origin main >&2
revision=$(git -C "$repo" rev-parse FETCH_HEAD)
release=$(git -C "$repo" rev-parse "$revision:Server/runtime")
[[ "$release" =~ ^[0-9a-f]{40}$ ]]
target="$base/releases/$release"
if [[ ! -d "$target" ]]; then
  git -C "$repo" checkout --detach --force "$revision" >&2
  stage=$(mktemp -d "$base/releases/staging.XXXXXXXX")
  trap 'rm -rf -- "$stage"' EXIT
  cp -a "$repo/Server/runtime/." "$stage/"
  # Runtime releases must contain ordinary files/directories only.
  if [[ -n $(find "$stage" ! -type f ! -type d -print -quit) ]]; then
    echo 'Unsupported file type in runtime release' >&2; exit 1
  fi
  cd "$stage"
  if [[ -f package.json ]]; then
    test -f package-lock.json
    npm ci --omit=dev --ignore-scripts --no-audit --no-fund >&2
  fi
  node --check server.mjs >&2
  chmod -R a+rX "$stage"
  mv "$stage" "$target"
  trap - EXIT
fi
printf '%s\n' "$release"
