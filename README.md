# TFR Online Menu

Runtime prototype mod for the IL2CPP build of Touhou Fumo Racing.

Current behavior:

- restores the disabled `OnlineButton` in `menu2`;
- opens a fallback multiplayer panel instead of loading the missing `mpmenu` scene;
- initializes the game's original `MultiplayerSystem`, which activates the disabled
  `Multiplayer` hierarchy before accessing `FRNetworkManager`;
- explicitly restores the shipped `MANAGERS/Multiplayer` root when the missing
  `mpmenu` scene cannot perform that activation;
- exposes nickname, host address, and map;
- invokes the game's existing Mirror `StartHost`, `StartClient`, and stop paths;
- prevents repeated Host/Join calls while Mirror is already active;
- exposes the original host-only race start path with map and lap settings;
- restores the local `GameManager` player required by the original network scene loader;
- registers the shipped `NetworkingPlayer` and `ServerSync` Mirror prefabs;
- prepares the shipped `QuickRace` game-mode prefab and selected circuit metadata so
  the original network countdown can start without disconnecting the host client;
- prepares the same local `QuickRace` mode on remote clients before they receive the
  network start-sequence message;
- binds the visible local player's original `PlayerInput` control scheme and keyboard
  or gamepad to the host/client racer;
- explicitly completes the slot-0 `PlayerInput` binding when the restored input
  manager's original player-joined callback stops after creating the local racer;
- allows networking to start with a visible warning if only the physical driving-input
  binding fails, instead of silently leaving the instance offline;
- sends the original race-ready callback when the disabled menu flow cannot do so;
- shows the host's connected player count in the panel status line;
- mutes game audio and stops Mirror during a normal application quit to avoid the
  loud final-frame sound;
- provides `F8` as a fallback panel toggle.

The project references MelonLoader-generated interop assemblies from:

`F:\Projects\TFR_OL\modded test\MelonLoader\Il2CppAssemblies`

## Manual LAN test

1. Start the game normally on the host, open `Online`, enter a nickname, map, and
   lap count, then press `Host`.
2. Start another instance or another installation on the client. Open `Online`, set
   the host address to the host machine's LAN IPv4 address, and press `Join`.
3. Wait until the host status reads `Network mode: Host | Players: 2`.
4. Press `Start Race` on the host. Both instances should load the same circuit; the
   host is racer 0 and the client is racer 1.
5. If testing across two machines, allow inbound UDP port `7777` for the game on the
   host firewall. Internet testing additionally requires forwarding UDP `7777` to
   the host or using a suitable VPN/tunnel.

Two instances on one PC are sufficient to test lobby connection, scene sync, and
race start. Only the focused visible window receives useful keyboard input, so two
machines or separate controllers are better for an actual driving test.

## Headless diagnostics

Diagnostics support `--tfr-online-init-only`, `--tfr-online-host-smoke`,
`--tfr-online-client-smoke`, `--tfr-online-expected-players=N`, and
`--tfr-online-address=ADDRESS`. The narrower `--tfr-online-racer-init-smoke` and
`--tfr-online-sync-spawn-smoke` switches isolate the final race-start stages.

The v0.4.4 two-process headless smoke test connects a host and client over KCP,
loads `forest` on both, completes both ready handshakes, assigns racer indices 0 and
1, spawns `ServerSync`, and keeps the remote client connected through the original
five-second start sequence.

Version 0.4.5 also replaces the fragile `AddDefaultPlayer` dependency with an
explicit `AddPlayer`/`SetInput` handoff for visible instances. Headless diagnostics
still intentionally create no physical input device.
