#!/bin/bash
set -euo pipefail
[[ $EUID == 0 ]] || { echo 'Run with sudo bash'; exit 1; }
# One-time opt-in. This affects the fishing service and its TCP game port only.
systemctl is-active --quiet firewalld || { echo 'firewalld is not active; inspect the VM firewall before exposing the game port.'; exit 1; }
install -d -m 755 /etc/systemd/system/fishing-server.service.d
cat > /etc/systemd/system/fishing-server.service.d/multiplayer.conf <<'UNIT'
[Service]
Environment=MULTIPLAYER_PUBLIC=1 GAME_PORT=3001 GAME_HOST=0.0.0.0
UNIT
systemctl daemon-reload
systemctl start fishing-update.service
systemctl restart fishing-server.service
ready=0
for attempt in {1..20}; do
  if curl -fsS --max-time 2 http://127.0.0.1:3001/health; then ready=1; break; fi
  sleep 1
done
[[ $ready == 1 ]] || { echo 'Game listener failed. Check journalctl -u fishing-server.'; exit 1; }
interface=$(ip -o -4 route show to default | awk '{print $5; exit}')
zone=$(firewall-cmd --get-zone-of-interface="$interface" || true)
if [[ -z "$zone" || "$zone" == 'no zone' ]]; then zone=$(firewall-cmd --get-default-zone); fi
firewall-cmd --zone="$zone" --permanent --add-port=3001/tcp
firewall-cmd --zone="$zone" --add-port=3001/tcp
echo
echo 'Fishing multiplayer enabled. Also add an Oracle VCN ingress rule: TCP destination port 3001.'
