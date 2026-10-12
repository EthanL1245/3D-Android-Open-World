# Fishing guest multiplayer

`MENU / TRAVEL → MULTIPLAYER` now offers **Open Server**, **Join Server**, an
8-character room code, **Copy Room Code**, and **Leave Server**. Opening creates a
room on Oracle; it does not host a process on the player's device. Up to eight
players per room can see animated, interpolated avatars walking/swimming around
the fishing islands. Players keep their existing world positions when joining:
travel to the same island to meet. Offline play makes no network requests.

## Scope

This first version is shared player presence, not full server-authoritative game
simulation. The server owns room membership, tokens, capacity and lifecycle,
validates message shape/world bounds, and relays client-reported positions. Local
movement physics stays on each device. Fish, catches, ripples, boats, inventories,
economy, home worlds and saves are not synchronized. No accounts or cloud saves.
Other avatars are hidden in shop/home dimensions. There is no player collision.

The host must stay connected. Leaving as host closes the room. A disconnected
host expires after 30 seconds. Transient request failures retry; after three
failures the game returns to offline and players can open/join again. Backend
restarts clear rooms; open a new room afterward. Android backgrounding can expire
a session. Protocol and Application.version must match on both devices; install
the same newly built game on both devices for testing.

## One-time network activation on the existing Oracle VM

The private deployment health service remains on `127.0.0.1:3000`. Game traffic
uses TCP **3001** and is off until explicitly enabled. Download
`Server/ops/enable-multiplayer.sh` from the reviewed commit and run it with sudo.
It fetches the latest runtime through the existing updater, enables the game
listener with a systemd override, restarts the fishing service, and opens TCP 3001
in the active VM firewall zone. It stops with an explanation if firewalld is not
active. It does not change the tank VM.

In Oracle Console, open **fishing-game-vcn → fishing-game-public-subnet → its
Security List → Add Ingress Rules**. Add a stateful rule (Stateless unchecked):
source CIDR `0.0.0.0/0`, IP protocol TCP, source port All, destination port `3001`.
Do not replace existing SSH rules. No router forwarding or player private keys
are needed. From another machine, check `http://129.153.55.216:3001/health`.

The initial guest-only endpoint is plain HTTP, not encrypted. Use pseudonyms;
do not add account credentials or private saved data to this protocol. Session
tokens are ephemeral and may be intercepted on an untrusted network. Before
accounts or public production release, add HTTPS and change the client endpoint.
The editor/build helper enables HTTP and Android internet permission for this
prototype; remove the HTTP allowance when moving to HTTPS. This implementation
uses UnityWebRequest on Android/desktop with no third-party networking package.
WebGL is not supported by this initial setup (no browser CORS/mixed-content setup).

## Build and test

Pull `main` into Unity, let scripts finish compiling, then Build & Run on both
devices (or use Editor play mode for one player). No scene editing is needed.
Open MENU / TRAVEL, select MULTIPLAYER, enter a guest name, and choose Open Server.
On the other device, enter that code and choose Join Server. Close the menu and
walk/jump/swim: the other avatar should follow with interpolation. Travel to
another island and confirm both clients agree on where the avatar is. Travel to
Home and confirm the avatar hides; return to the fishing world to show it again.
Check Leave Server, an invalid code, and a disconnected network. Confirm offline
movement and fishing continue independently afterward.

Automated backend tests: `node --test Server/tests/sessions.test.mjs` (Node 24).
These cover actual HTTP open/join/movement exchange, room isolation, token
rejection, invalid/version-mismatched requests, capacity, host/guest leave,
expiry, and attempt limiting. Unity compilation, Android permissions/UI, and
real two-device visual testing still require Unity/devices; Node tests cannot
validate those. No live Oracle test is claimed until the one-time setup runs.

## Automatic deployment

Push deployable code under `Server/runtime/` to `main`. The updater checks about
every five minutes, shallow-fetching only this folder's blobs. Unity/ops-only
changes do not restart the service. Entry point: `server.mjs`. Keep the private
GET `/health` response `{ "ok": true, "release": process.env.RELEASE_ID }`.
If dependencies are added, commit both package.json and package-lock.json.
Production dependencies install with lifecycle scripts disabled.

Updates stage separately, validate entrypoint syntax, switch releases and restart.
A failed health check restores the last successful release and suppresses that
failed runtime until code changes. Interrupted initial installs can be retried.
The service restarts after crashes/reboots. Runtime updates do not update Android
builds, OS/Node, or ops scripts. Releases are retained; monitor disk as they grow.

Runtime user: `fishing-server`; deployment user: `fishing-deploy`. Runtime code
is read-only to the game process. Future writable data belongs in
`DATA_DIR=/var/lib/fishing-server`. Coordinator metadata uses
`/var/lib/fishing-deployment`, persistently labeled `init_var_lib_t` on SELinux.
The existing pinned `Server/ops/install.sh` handles first installation/ops updates.

```sh
curl http://127.0.0.1:3000/health
curl http://127.0.0.1:3001/health
systemctl status fishing-server fishing-update.timer --no-pager
sudo journalctl -u fishing-server -u fishing-update -n 80 --no-pager
sudo systemctl start fishing-update
```

Pause deployment: `sudo systemctl stop fishing-update.timer`.
Retry an unchanged failed version after fixing its environment:
`sudo rm /var/lib/fishing-deployment/failed-release`, then start fishing-update.
