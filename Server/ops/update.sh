#!/bin/bash
set -euo pipefail
base=/opt/fishing-server
state=/var/lib/fishing-deployment
exec 9>/run/fishing-update.lock
flock -n 9 || exit 0
release=$(runuser -u fishing-deploy -- /usr/local/lib/fishing-server/prepare.sh)
[[ "$release" =~ ^[0-9a-f]{40}$ ]]
target="$base/releases/$release"
previous=''
if [[ -f "$state/successful-release" ]]; then
  prior=$(cat "$state/successful-release")
  [[ "$prior" =~ ^[0-9a-f]{40}$ ]]
  previous="$base/releases/$prior"
  if [[ "$prior" == "$release" && -f "$state/release.env" && $(readlink "$base/current" || true) == "$target" ]]; then
    exit 0
  fi
fi
[[ ! -f "$state/failed-release" || $(cat "$state/failed-release") != "$release" ]] || exit 0
switch_to() {
  ln -sfn "$1" "$base/current.next"
  mv -Tf "$base/current.next" "$base/current"
}
healthy() {
  local expected="$1"
  for attempt in {1..20}; do
    if curl -fsS --max-time 2 http://127.0.0.1:3000/health 2>/dev/null |
      /usr/bin/node -e 'let s="";process.stdin.on("data",c=>s+=c);process.stdin.on("end",()=>{try {const h=JSON.parse(s);process.exit(h.ok===true && h.release===process.argv[1]?0:1)}catch{process.exit(1)}})' "$expected"; then
      return 0
    fi
    sleep 1
  done
  return 1
}
# Prepare configuration before switching code; a failed write leaves code alone.
write_environment() {
  printf 'RELEASE_ID=%s\n' "$1" > "$state/release.env.next"
  mv -f "$state/release.env.next" "$state/release.env"
}
write_environment "$release"
switch_to "$target"
systemctl reset-failed fishing-server.service || true
if systemctl restart fishing-server.service && healthy "$release"; then
  printf '%s\n' "$release" > "$state/successful-release.next"
  mv -f "$state/successful-release.next" "$state/successful-release"
  rm -f "$state/failed-release"
  echo "Deployed fishing runtime $release"
else
  printf '%s\n' "$release" > "$state/failed-release"
  if [[ -n "$previous" ]]; then
    switch_to "$previous"
    write_environment "${previous##*/}"
    systemctl reset-failed fishing-server.service || true
    systemctl restart fishing-server.service
    healthy "${previous##*/}" || echo 'Rollback health check failed' >&2
  else
    systemctl stop fishing-server.service
    rm -f "$base/current"
  fi
  echo "Release $release failed health check; previous release restored if available" >&2
  exit 1
fi
