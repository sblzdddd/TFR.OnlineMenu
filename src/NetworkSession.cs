using Il2Cpp;
using UnityEngine;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod
{
    private float _readySignalAt = -1;
    private int _readySignalAttempts;

    private void HandleNetworkSceneLoaded(string sceneName)
    {
        if (!IsPlayableScene(sceneName))
        {
            return;
        }

        if (Il2CppMirror.NetworkClient.active && !EnsureNetworkGameMode(sceneName))
        {
            LoggerInstance.Error(_message);
        }

        _readySignalAttempts = 0;
        _readySignalAt = Time.unscaledTime + 0.5f;
    }

    private void UpdateRaceReady()
    {
        if (_readySignalAt >= 0 && Time.unscaledTime >= _readySignalAt)
        {
            SendRaceReadyWhenAvailable();
        }
    }

    private bool InitializeMultiplayerSystem()
    {
        var multiplayerManager = MultiplayerManager.instance;
        if (!multiplayerManager)
        {
            _message = "MultiplayerManager is not available.";
            return false;
        }

        multiplayerManager._system ??= new MultiplayerSystem();

        var multiplayerRoot = FindMultiplayerRoot();
        if (!multiplayerRoot)
        {
            _message = "The MANAGERS/Multiplayer root was not found.";
            return false;
        }

        multiplayerRoot!.SetActive(true);
        if (!FRNetworkManager.instancia)
        {
            _message = "FRNetworkManager did not initialize.";
            return false;
        }

        _message = "Original multiplayer system initialized.";
        return true;
    }

    private static GameObject? FindMultiplayerRoot()
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!candidate || candidate.name != "Multiplayer")
            {
                continue;
            }

            var parent = candidate.transform.parent;
            if (candidate.scene.IsValid() && candidate.scene.isLoaded && parent && parent.name == "MANAGERS")
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsPlayableScene(string sceneName)
    {
        return !sceneName.Equals("mainscene", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("translations", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("splash", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("loading", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("render", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("menu2", StringComparison.OrdinalIgnoreCase);
    }

    private void SendRaceReadyWhenAvailable()
    {
        if (!Il2CppMirror.NetworkClient.active)
        {
            _readySignalAt = -1;
            return;
        }

        var localPlayer = FRNetworkPlayer.localPlayer;
        if (localPlayer && localPlayer.isServerReady)
        {
            _readySignalAt = -1;
            return;
        }

        var gameState = FRNetGameState.instance;
        if (!GameManager.inRace || !localPlayer || !gameState)
        {
            _readySignalAt = Time.unscaledTime + 0.25f;
            return;
        }

        gameState.OnLevelStart();
        _readySignalAttempts++;
        _readySignalAt = _readySignalAttempts < 3
            ? Time.unscaledTime + 1.5f
            : -1;
    }

    private FRNetworkManager? PrepareNetworkStart()
    {
        if (!InitializeMultiplayerSystem())
        {
            return null;
        }

        var nickname = _nickname.Trim();
        if (string.IsNullOrWhiteSpace(nickname) || nickname.Length >= 64)
        {
            _message = "Nickname must contain 1-63 characters.";
            return null;
        }

        var client = FRNetworkClient.instance;
        if (client)
        {
            client.SetNickname(nickname);
        }

        var manager = FRNetworkManager.instancia;
        return EnsureNetworkPrefabs(manager) && EnsureLocalGamePlayer()
            ? manager
            : null;
    }

    private bool EnsureNetworkPrefabs(FRNetworkManager manager)
    {
        var server = FRNetworkServer.instance;
        var gameState = FRNetGameState.instance;
        var spawnPrefabs = manager.spawnPrefabs;
        if (!server || !gameState || spawnPrefabs is null)
        {
            _message = "The game's network prefab registry is unavailable.";
            return false;
        }

        var playerPrefab = server._playerPrefab ? server._playerPrefab.gameObject : null;
        var gameSyncPrefab = gameState._gameSyncPrefab ? gameState._gameSyncPrefab.gameObject : null;
        if (!playerPrefab || !gameSyncPrefab)
        {
            _message = "A required multiplayer prefab is missing.";
            return false;
        }

        if (!spawnPrefabs.Contains(playerPrefab!))
        {
            spawnPrefabs.Add(playerPrefab!);
        }

        if (!spawnPrefabs.Contains(gameSyncPrefab!))
        {
            spawnPrefabs.Add(gameSyncPrefab!);
        }

        return true;
    }

    private void StartHost()
    {
        var manager = PrepareNetworkStart();
        if (!manager)
        {
            return;
        }

        manager!.StartHost();
        _message = "Host started on UDP 7777. Add players, then press Start Race.";
    }

    private void StartClient()
    {
        var address = _address.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            _message = "Host address cannot be empty.";
            return;
        }

        var manager = PrepareNetworkStart();
        if (!manager)
        {
            return;
        }

        manager!.networkAddress = address;
        manager.StartClient();
        _message = $"Connecting to {address}:7777 as {_nickname.Trim()}...";
    }

    private void StopNetwork()
    {
        var manager = FRNetworkManager.instancia;
        if (!manager)
        {
            _message = "FRNetworkManager is not available.";
            return;
        }

        if (Il2CppMirror.NetworkServer.active)
        {
            manager.StopHost();
        }
        else if (Il2CppMirror.NetworkClient.active)
        {
            manager.StopClient();
        }

        var multiplayerManager = MultiplayerManager.instance;
        if (multiplayerManager && multiplayerManager._system is not null)
        {
            multiplayerManager._system.Close();
            multiplayerManager._system = null;
        }

        CleanupNetworkGameMode();
        _message = "Network session stopped.";
    }

    private static string GetNetworkStatus()
    {
        if (Il2CppMirror.NetworkServer.active)
        {
            var server = FRNetworkServer.instance;
            var playerCount = server ? server.GetPlayers().Count : 0;
            return $"Network mode: Host | Players: {playerCount}";
        }

        return Il2CppMirror.NetworkClient.active
            ? "Network mode: Client"
            : "Network mode: Offline";
    }
}
