using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TFROnlineMenu.Ui;
using UnityEngine;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod
{
    private GameMode? _networkGameMode;

    internal void StartRaceFromSelection(GameModeManager.SGameModeProperties props)
    {
        var netPlayers = OnlineSelection.GetNetworkPlayers();
        if (netPlayers.Count < 2)
        {
            Message = "At least two players are required to start an online race.";
            LoggerInstance.Warning(Message);
            return;
        }

        CircuitData? circuit = null;
        if (props._maps is not null && props._maps.Length > 0)
        {
            circuit = props._maps[0];
        }

        var map = circuit is not null ? circuit._scene : Map.Trim();
        var laps = props._laps > 0 ? props._laps : 3;
        if (!EnsureNetworkGameMode(map, netPlayers.Count, circuit))
        {
            return;
        }

        var gameState = FRNetGameState.instance;
        if (!gameState)
        {
            Message = "FRNetGameState is not available.";
            return;
        }

        var roster = new Il2CppReferenceArray<GamePlayer>(netPlayers.Count);
        for (var index = 0; index < netPlayers.Count; index++)
        {
            var netPlayer = netPlayers[index];
            var mp = new GameMPPlayer();
            mp.InitMP(netPlayer);
            var info = netPlayer.Network_info._racerInfo;
            if (info._character >= 0)
            {
                mp.character = info._character;
                mp.skin = info._skin;
                mp.vehicle = info._vehicle;
            }

            roster[index] = mp;
        }

        var settings = gameState._settings;
        settings._players = roster;
        settings._level = map;
        settings._laps = laps;
        settings._disableAutoStart = false;
        gameState._settings = settings;
        gameState.map = map;
        gameState.StartGame(settings);
        OnlineSelection.Stop();
        OnlineRaceMenu.Suspend();
        Message = $"Starting {map} with {netPlayers.Count} player(s), {laps} lap(s)...";
    }

    private bool EnsureNetworkGameMode(string map, int maxPlayers = 0, CircuitData? circuit = null)
    {
        var gameModeManager = GameModeManager.instance;
        if (!gameModeManager)
        {
            Message = "GameModeManager is not available.";
            return false;
        }

        if (gameModeManager.currentGameMode)
        {
            return true;
        }

        var prefabObject = ResourcesManager.Load<GameObject>("GameModes/QuickRace");
        var prefab = prefabObject ? prefabObject.GetComponent<GameMode>() : null;
        if (!prefab)
        {
            Message = "The QuickRace prefab could not be loaded.";
            return false;
        }

        if (circuit is null)
        {
            var circuits = GameManager._circuits;
            for (var index = 0; circuits is not null && index < circuits.Count; index++)
            {
                if (circuits[index]._scene.Equals(map, StringComparison.OrdinalIgnoreCase))
                {
                    circuit = circuits[index];
                    break;
                }
            }
        }

        if (circuit is null)
        {
            Message = $"Unknown map '{map}'.";
            return false;
        }

        var instance = UnityEngine.Object.Instantiate(prefab!, Vector3.zero, Quaternion.identity)!;
        var playlist = new Il2CppReferenceArray<CircuitData>(1);
        playlist[0] = circuit;
        instance._playlist = playlist;
        instance._playlistIndex = 0;
        if (maxPlayers > 0)
        {
            instance._maxPlayers = maxPlayers;
        }

        UnityEngine.Object.DontDestroyOnLoad(instance.gameObject);
        gameModeManager._currentGM = instance;
        _networkGameMode = instance;
        return true;
    }

    private void CleanupNetworkGameMode()
    {
        OnlineSelection.Stop();
        if (!_networkGameMode)
        {
            _networkGameMode = null;
            return;
        }

        var gameModeManager = GameModeManager.instance;
        if (gameModeManager && gameModeManager.currentGameMode == _networkGameMode)
        {
            gameModeManager._currentGM = null;
        }

        UnityEngine.Object.Destroy(_networkGameMode!.gameObject);
        _networkGameMode = null;
    }
}
