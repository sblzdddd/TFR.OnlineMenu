using Il2Cpp;
using UnityEngine;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod
{
    private GameMode? _networkGameMode;

    private void StartRace()
    {
        if (!Il2CppMirror.NetworkServer.active)
        {
            _message = "Start Race is available only on the server.";
            return;
        }

        var server = FRNetworkServer.instance;
        var gameState = FRNetGameState.instance;
        if (!server || !gameState)
        {
            _message = "Server or game state is not ready.";
            return;
        }

        var playerCount = server.GetPlayers().Count;
        if (playerCount == 0)
        {
            _message = "Waiting for the host player.";
            return;
        }

        var map = _map.Trim();
        if (map.Length == 0 || !int.TryParse(_laps, out var laps) || laps is < 1 or > 99)
        {
            _message = "Choose a map and set laps from 1 to 99.";
            return;
        }

        if (!EnsureNetworkGameMode(map))
        {
            return;
        }

        gameState.map = map;
        gameState._settings._laps = laps;
        gameState.StartGame(gameState._settings);

        _showPanel = false;
        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
        _message = $"Starting {map} with {playerCount} player(s), {laps} lap(s)...";
    }

    private bool EnsureNetworkGameMode(string map)
    {
        var gameModeManager = GameModeManager.instance;
        if (!gameModeManager)
        {
            _message = "GameModeManager is not available.";
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
            _message = "The QuickRace prefab could not be loaded.";
            return false;
        }

        CircuitData? circuit = null;
        var circuits = GameManager._circuits;
        for (var index = 0; circuits is not null && index < circuits.Count; index++)
        {
            if (circuits[index]._scene.Equals(map, StringComparison.OrdinalIgnoreCase))
            {
                circuit = circuits[index];
                break;
            }
        }

        if (circuit is null)
        {
            _message = $"Unknown map '{map}'.";
            return false;
        }

        var instance = UnityEngine.Object.Instantiate(prefab!, Vector3.zero, Quaternion.identity)!;
        var playlist = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<CircuitData>(1);
        playlist[0] = circuit;
        instance._playlist = playlist;
        instance._playlistIndex = 0;
        UnityEngine.Object.DontDestroyOnLoad(instance.gameObject);

        gameModeManager._currentGM = instance;
        _networkGameMode = instance;
        return true;
    }

    private void CleanupNetworkGameMode()
    {
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
