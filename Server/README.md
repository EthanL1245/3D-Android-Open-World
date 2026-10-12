# Fishing backend deployment

This is the deployment foundation, not completed Unity multiplayer. The initial
runtime serves a loopback-only `/health` endpoint. It explicitly reports
`multiplayerReady: false`. No accounts, cloud saves, public game port or client
networking are enabled by this commit.

## One-time Oracle Linux 9 setup

Prerequisites: Node.js 24+, npm, and sufficient swap on the 1 GB Micro VM.
Download `Server/ops/install.sh` from a specific reviewed commit, then run:

```sh
sudo bash install.sh THE_40_CHARACTER_COMMIT_SHA
```

Installer adds Git if needed, using only BaseOS/AppStream repositories. It
creates two unprivileged users, a fishing-server systemd service and a five-minute
update timer. No SSH private key or GitHub token is needed for this public repo.
It does not change the tank server, firewall or Oracle network rules.

## Future server changes

Push deployable code under `Server/runtime/` to `main`. The updater shallow-fetches
the public repository and checks out only this folder, avoiding Unity asset blobs.
Only a changed runtime tree triggers a restart. Ops and Unity changes alone do
not restart the service. Use `server.mjs` as the entrypoint. Keep GET `/health`
returning `{ "ok": true, "release": process.env.RELEASE_ID }` for deployment checks.
If dependencies are added, commit both package.json and package-lock.json.
Production dependencies are installed with lifecycle scripts disabled.

Updates stage separately while the old server runs, validate entrypoint syntax,
then switch releases and restart. The health check must report the expected
release. Failure restores the previous version; the failed tree is skipped until
runtime code changes again. The first install has no previous version to restore.
Network/download failures leave the running release alone. Releases are retained
on disk for now; monitor disk use as the backend grows.

The app restarts after crashes and reboots. Restarting a future multiplayer
runtime will disconnect sessions; clients must reconnect. Automatic updates cover
compatible server code, not Android builds, OS/Node upgrades, or changed systemd
configuration. Ops script updates require rerunning the pinned installer.

Runtime runs as `fishing-server` with read-only code, limited memory, and a writable
`DATA_DIR` at `/var/lib/fishing-server`. Deployment runs as a separate account;
the root-owned coordinator manages service restart and rollback. Neither game
traffic nor the runtime account can modify the updater. Do not store secrets or
player data in Git. Add TLS and game-specific network access when implementing
the client protocol; the foundation intentionally listens only on 127.0.0.1.

## Diagnostics

```sh
curl http://127.0.0.1:3000/health
systemctl status fishing-server fishing-update.timer --no-pager
sudo journalctl -u fishing-server -u fishing-update -n 80 --no-pager
sudo systemctl start fishing-update
```

To pause automatic updates: `sudo systemctl stop fishing-update.timer`.
To retry an unchanged failed release after fixing an environmental problem:
`sudo rm /var/lib/fishing-deployment/failed-release`, then start fishing-update.

Deployment metadata lives in `/var/lib/fishing-deployment`, labeled
`init_var_lib_t` on SELinux hosts. The installer registers this exact directory
mapping persistently and restores its labels without disabling enforcement. A
success marker is written only after the health check passes, so an interrupted
first install can be retried even when the current symlink already exists.
