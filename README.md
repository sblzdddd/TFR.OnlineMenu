# TFR Online Menu

MelonLoader mod for the IL2CPP build of Touhou Fumo Racing. It restores the
disabled online entry point and reconnects the game's shipped Mirror networking
objects without modifying `GameAssembly.dll`.

## Current behavior

- Restores `OnlineButton` when `menu2` finishes initializing; `F8` remains a
  fallback panel toggle.
- Activates the shipped `MANAGERS/Multiplayer` hierarchy and initializes the
  original `MultiplayerSystem` before accessing `FRNetworkManager`.
- Provides nickname, host address, map, laps, Host, Join, Stop, and host-only
  Start Race controls.
- Uses the game's existing Mirror host/client/stop paths and registers the
  shipped `NetworkingPlayer` and `ServerSync` prefabs.
- Creates the shipped `QuickRace` mode with the selected circuit on host and
  client so the original countdown and scene synchronization can run.
- Restores slot-0 `PlayerInput` from the active keyboard or gamepad. HarmonyX
  postfix patches complete the binding at `RacingInputManager.OnPlayerJoined`
  and initialize the controller at `HumanGamePlayer.OnPossessed`; there is no
  frame-by-frame driving-input repair loop.
- Sends the original race-ready callback when the missing multiplayer menu flow
  cannot send it, and reports connected player count in the panel.
- Stops Mirror and mutes game audio during normal application shutdown.

`src/SplashFucker.cs` is a separate startup-screen patch and is intentionally
outside the online-menu flow.

## Source layout

- `OnlineMenuMod.cs`: MelonLoader lifecycle and the multiplayer panel.
- `NetworkSession.cs`: multiplayer initialization, Host/Join/Stop, and ready handshake.
- `LocalInput.cs`: slot-0 input binding and possession handling.
- `RaceSession.cs`: race settings, QuickRace creation, start, and cleanup.
- `InputPatches.cs`: the two HarmonyX lifecycle hooks.

## Build

The repository defaults `GameRoot` to:

`F:\Projects\TFR_OL\modded test`

Run:

```powershell
dotnet build
```

The build reads MelonLoader and generated IL2CPP assemblies from `GameRoot` and
writes only `TFR.OnlineMenu.dll` and its symbols to `GameRoot\Mods`. To build
against another installation:

```powershell
dotnet build -p:GameRoot="X:\path\to\Touhou Fumo Racing"
```

The Harmony patches use attributes and rely on MelonLoader's automatic
`PatchAll()` call. This follows the
[MelonLoader patching guide](https://melonwiki.xyz/#/modders/patching); IL2CPP
transpilers are not used.

## Manual LAN test

1. Start the host normally, open `Online`, enter nickname, map, and laps, then
   press `Host`.
2. Start a second instance or installation, enter the host machine's LAN IPv4
   address, and press `Join`.
3. Wait for `Network mode: Host | Players: 2`, then press `Start Race` on the
   host.
4. Confirm both instances load the same circuit and that each visible instance
   can steer, accelerate, pause, and return to its menu.

Allow inbound UDP port `7777` on the host for two-machine LAN testing. Internet
hosting also requires UDP `7777` forwarding or an appropriate VPN/tunnel. Two
instances on one PC can verify connection and scene synchronization, but only
the focused window receives useful keyboard input.
