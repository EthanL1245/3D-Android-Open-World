#!/bin/bash
set -euo pipefail
base=/opt/fishing-server
exec 9>/run/fishing-update.lock
flock -n 9 || exit 0
release=$(runuser -u fishing-deploy -- /usr/local/lib/fishing-server/prepare.sh)
[[ "$release" =~ ^[0-9a-f]{40}$ ]]
target="$base/releases/$release"
previous=$(readlink "$base/current" || true)
[[ "$previous" != "$target" ]] || exit 0
[[ ! -f "$base/failed-release" || $(cat "$base/failed-release") != "$release" ]] || exit 0
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
switch_to "$target"
printf 'RELEASE_ID=%s\n' "$release" > "$base/release.env"
systemctl reset-failed fishing-server.service || true
if systemctl restart fishing-server.service && healthy "$release"; then
  rm -f "$base/failed-release"
  echo "Deployed fishing runtime $release"
else
  printf '%s\n' "$release" > "$base/failed-release"
  if [[ -n "$previous" ]]; then
    switch_to "$previous"
    printf 'RELEASE_ID=%s\n' "${previous##*/}" > "$base/release.env"
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
