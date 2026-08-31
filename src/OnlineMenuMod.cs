using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TFROnlineMenu;

public sealed class OnlineMenuMod : MelonMod
{
    private const float HookIntervalSeconds = 1.0f;

    private readonly Rect _panelRect = new(24, 24, 420, 370);
    private Button? _onlineButton;
    private UnityAction? _onlineButtonAction;
    private Il2CppSystem.Action? _clientDisconnectedDiagnostic;
    private Il2CppSystem.Action<Il2CppSystem.Exception>? _clientErrorDiagnostic;
    private GameMode? _networkGameMode;
    private float _nextHookAttempt;
    private float _runtimeProbeAt = -1;
    private float _diagnosticInitializeAt = -1;
    private float _diagnosticRaceAt = -1;
    private float _raceProbeAt = -1;
    private float _raceProbeUntil = -1;
    private float _readySignalAt = -1;
    private float _localDrivingRepairAt = -1;
    private float _localDrivingRepairUntil = -1;
    private int _diagnosticRaceAttempts;
    private int _readySignalAttempts;
    private int _localDrivingRepairAttempts;
    private bool _showPanel;
    private bool _initializeOnlyDiagnostic;
    private bool _hostSmokeDiagnostic;
    private bool _clientSmokeDiagnostic;
    private bool _racerInitializeDiagnostic;
    private bool _syncSpawnDiagnostic;
    private bool _networkDiagnosticsHooked;
    private int _diagnosticExpectedPlayers = 1;
    private string _lastSceneName = "unknown";
    private string _nickname = "Fumo";
    private string _address = "127.0.0.1";
    private string _map = "forest";
    private string _laps = "3";
    private string _message = "Click Host or Join to begin.";
    private string? _localInputWarning;

    public override void OnInitializeMelon()
    {
        var commandLine = Environment.GetCommandLineArgs();
        _syncSpawnDiagnostic = commandLine
            .Any(argument => argument.Equals("--tfr-online-sync-spawn-smoke", StringComparison.OrdinalIgnoreCase));
        _racerInitializeDiagnostic = _syncSpawnDiagnostic || commandLine
            .Any(argument => argument.Equals("--tfr-online-racer-init-smoke", StringComparison.OrdinalIgnoreCase));
        _hostSmokeDiagnostic = _racerInitializeDiagnostic || commandLine
            .Any(argument => argument.Equals("--tfr-online-host-smoke", StringComparison.OrdinalIgnoreCase));
        _clientSmokeDiagnostic = commandLine
            .Any(argument => argument.Equals("--tfr-online-client-smoke", StringComparison.OrdinalIgnoreCase));
        _initializeOnlyDiagnostic = _hostSmokeDiagnostic || _clientSmokeDiagnostic || commandLine
            .Any(argument => argument.Equals("--tfr-online-init-only", StringComparison.OrdinalIgnoreCase));

        var expectedPlayersArgument = commandLine.FirstOrDefault(argument =>
            argument.StartsWith("--tfr-online-expected-players=", StringComparison.OrdinalIgnoreCase));
        if (expectedPlayersArgument is not null &&
            int.TryParse(expectedPlayersArgument.Split('=', 2)[1], out var expectedPlayers))
        {
            _diagnosticExpectedPlayers = Math.Clamp(expectedPlayers, 1, 4);
        }

        var addressArgument = commandLine.FirstOrDefault(argument =>
            argument.StartsWith("--tfr-online-address=", StringComparison.OrdinalIgnoreCase));
        if (addressArgument is not null)
        {
            _address = addressArgument.Split('=', 2)[1].Trim();
        }

        if (_clientSmokeDiagnostic)
        {
            _nickname = "SmokeClient";
        }
        LoggerInstance.Msg("TFR Online Menu initialized. Press F8 if the Online button is unavailable.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        _onlineButton = null;
        _onlineButtonAction = null;
        _nextHookAttempt = 0;
        _lastSceneName = sceneName;
        _runtimeProbeAt = Time.unscaledTime + 0.75f;
        if (_initializeOnlyDiagnostic && sceneName.Equals("menu2", StringComparison.OrdinalIgnoreCase))
        {
            _diagnosticInitializeAt = Time.unscaledTime + 1.0f;
        }
        if (IsPlayableScene(sceneName))
        {
            if (Il2CppMirror.NetworkClient.active)
            {
                if (EnsureNetworkGameMode(sceneName))
                {
                    LoggerInstance.Msg(
                        $"Ensured local QuickRace mode before the network start sequence: {sceneName}.");
                }
                else
                {
                    LoggerInstance.Error(
                        $"Could not prepare a local game mode for network scene {sceneName}: {_message}");
                }
            }

            _readySignalAttempts = 0;
            _readySignalAt = Time.unscaledTime + 0.5f;
            if (!Application.isBatchMode && Il2CppMirror.NetworkClient.active)
            {
                _localDrivingRepairAttempts = 0;
                _localDrivingRepairAt = Time.unscaledTime + 0.5f;
                _localDrivingRepairUntil = Time.unscaledTime + 20.0f;
            }
            if (_clientSmokeDiagnostic)
            {
                _raceProbeAt = Time.unscaledTime + 0.25f;
                _raceProbeUntil = Time.unscaledTime + 20.0f;
            }
        }
        LoggerInstance.Msg($"Scene loaded: {sceneName} ({buildIndex})");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            TogglePanel();
        }

        if (Time.unscaledTime >= _nextHookAttempt)
        {
            _nextHookAttempt = Time.unscaledTime + HookIntervalSeconds;
            TryRestoreOnlineButton();
        }

        if (_runtimeProbeAt >= 0 && Time.unscaledTime >= _runtimeProbeAt)
        {
            _runtimeProbeAt = -1;
            ProbeNetworkRuntime();
        }

        if (_diagnosticInitializeAt >= 0 && Time.unscaledTime >= _diagnosticInitializeAt)
        {
            _diagnosticInitializeAt = -1;
            var initialized = TryInitializeMultiplayerSystem();
            LoggerInstance.Msg($"Initialize-only diagnostic result: {initialized}");
            _runtimeProbeAt = Time.unscaledTime + 0.75f;
            if (initialized && _hostSmokeDiagnostic)
            {
                TryStartHost();
                var manager = FRNetworkManager.instancia;
                if (manager && manager.mode == Il2CppMirror.NetworkManagerMode.Host)
                {
                    _diagnosticRaceAt = Time.unscaledTime + 1.0f;
                }
                else
                {
                    _diagnosticRaceAt = -1;
                    LoggerInstance.Error(
                        $"Host diagnostic did not start; current message: {_message}");
                }
            }
            else if (initialized && _clientSmokeDiagnostic)
            {
                TryStartClient();
            }
        }

        if (_diagnosticRaceAt >= 0 && Time.unscaledTime >= _diagnosticRaceAt)
        {
            var server = FRNetworkServer.instance;
            var serverPlayers = server ? server.GetPlayers() : null;
            var playerCount = serverPlayers is null ? 0 : serverPlayers.Count;
            if (playerCount >= _diagnosticExpectedPlayers)
            {
                _diagnosticRaceAt = -1;
                TryStartRace();
            }
            else if (++_diagnosticRaceAttempts < 120)
            {
                _diagnosticRaceAt = Time.unscaledTime + 1.0f;
            }
            else
            {
                _diagnosticRaceAt = -1;
                LoggerInstance.Error(
                    $"Host smoke test timed out waiting for {_diagnosticExpectedPlayers} player(s); " +
                    $"connected={playerCount}.");
            }
        }

        if (_raceProbeAt >= 0 && Time.unscaledTime >= _raceProbeAt)
        {
            ProbeRaceProgress();
            if (Time.unscaledTime < _raceProbeUntil)
            {
                _raceProbeAt = Time.unscaledTime + 1.0f;
            }
            else
            {
                _raceProbeAt = -1;
            }
        }

        if (_readySignalAt >= 0 && Time.unscaledTime >= _readySignalAt)
        {
            TrySendRaceReadyFallback();
        }

        if (_localDrivingRepairAt >= 0 && Time.unscaledTime >= _localDrivingRepairAt)
        {
            _localDrivingRepairAttempts++;
            if (TryEnsureLocalDrivingController())
            {
                _localDrivingRepairAt = -1;
            }
            else if (Time.unscaledTime < _localDrivingRepairUntil)
            {
                _localDrivingRepairAt = Time.unscaledTime + 0.5f;
            }
            else
            {
                LoggerInstance.Error(
                    $"Local driving input did not become ready after " +
                    $"{_localDrivingRepairAttempts} attempts.");
                _localDrivingRepairAt = -1;
            }
        }

    }

    public override void OnGUI()
    {
        if (!_showPanel)
        {
            return;
        }

        GUI.Box(_panelRect, "TFR Multiplayer Prototype");

        GUI.Label(new Rect(44, 62, 110, 24), "Nickname");
        _nickname = GUI.TextField(new Rect(154, 60, 260, 26), _nickname, 63);

        GUI.Label(new Rect(44, 98, 110, 24), "Host address");
        _address = GUI.TextField(new Rect(154, 96, 260, 26), _address, 255);

        GUI.Label(new Rect(44, 134, 110, 24), "Map");
        _map = GUI.TextField(new Rect(154, 132, 154, 26), _map, 64);

        GUI.Label(new Rect(318, 134, 44, 24), "Laps");
        _laps = GUI.TextField(new Rect(362, 132, 52, 26), _laps, 2);

        if (GUI.Button(new Rect(44, 174, 108, 34), "Host"))
        {
            TryStartHost();
        }

        if (GUI.Button(new Rect(166, 174, 108, 34), "Join"))
        {
            TryStartClient();
        }

        if (GUI.Button(new Rect(288, 174, 108, 34), "Stop"))
        {
            TryStopNetwork();
        }

        if (GUI.Button(new Rect(44, 220, 352, 36), "Start Race (Host)"))
        {
            TryStartRace();
        }

        GUI.Label(new Rect(44, 272, 352, 24), GetNetworkStatus());
        GUI.Label(new Rect(44, 302, 352, 62), _message);

        if (GUI.Button(new Rect(386, 28, 28, 24), "X"))
        {
            _showPanel = false;
        }
    }

    public override void OnApplicationQuit()
    {
        try
        {
            AudioListener.volume = 0.0f;
            AudioListener.pause = true;
            TryStopNetwork();
            LoggerInstance.Msg("Muted game audio and stopped networking for application shutdown.");
        }
        catch (Exception exception)
        {
            LoggerInstance.Warning($"Graceful application shutdown encountered: {exception.Message}");
        }
    }

    private void TryRestoreOnlineButton()
    {
        if (_onlineButton)
        {
            return;
        }

        var onlineObject = GameObject.Find("OnlineButton");
        if (!onlineObject)
        {
            return;
        }

        var button = onlineObject.GetComponent<Button>();
        if (!button)
        {
            LoggerInstance.Warning("OnlineButton exists but has no UnityEngine.UI.Button component.");
            return;
        }

        button.enabled = true;
        button.interactable = true;

        var selector = onlineObject.GetComponent<UIFRSelectable>();
        if (selector)
        {
            selector.enabled = true;
        }

        _onlineButtonAction = (UnityAction)TogglePanel;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(_onlineButtonAction);
        _onlineButton = button;

        LoggerInstance.Msg("Restored OnlineButton and attached the prototype multiplayer panel.");
    }

    private void TogglePanel()
    {
        _showPanel = !_showPanel;
        if (_showPanel)
        {
            TryInitializeMultiplayerSystem();
        }
    }

    private bool TryInitializeMultiplayerSystem()
    {
        try
        {
            var multiplayerManager = MultiplayerManager.instance;
            if (!multiplayerManager)
            {
                _message = "MultiplayerManager is not available.";
                return false;
            }

            if (multiplayerManager._system is null)
            {
                multiplayerManager._system = new MultiplayerSystem();
                LoggerInstance.Msg("Initialized the game's original MultiplayerSystem.");
            }

            var multiplayerRoot = FindMultiplayerRoot();
            if (multiplayerRoot is null || !multiplayerRoot)
            {
                _message = "The disabled MANAGERS/Multiplayer root was not found.";
                return false;
            }

            if (!multiplayerRoot.activeInHierarchy)
            {
                multiplayerRoot.SetActive(true);
                LoggerInstance.Msg("Activated the game's original MANAGERS/Multiplayer hierarchy.");
            }

            var networkManager = FRNetworkManager.instancia;
            if (!networkManager)
            {
                _message = "MultiplayerSystem started, but FRNetworkManager did not initialize.";
                return false;
            }

            _message = "Original multiplayer system initialized.";
            return true;
        }
        catch (Exception exception)
        {
            ReportFailure("Multiplayer initialization failed", exception);
            return false;
        }
    }

    private static GameObject? FindMultiplayerRoot()
    {
        var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var index = 0; index < gameObjects.Length; index++)
        {
            var candidate = gameObjects[index];
            if (!candidate || candidate.name != "Multiplayer")
            {
                continue;
            }

            var scene = candidate.scene;
            var parent = candidate.transform.parent;
            if (scene.IsValid() && scene.isLoaded && parent && parent.name == "MANAGERS")
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

    private void TrySendRaceReadyFallback()
    {
        try
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

            if (_racerInitializeDiagnostic)
            {
                _readySignalAt = -1;
                ProbeNetworkRacerInitialization();
                return;
            }

            gameState.OnLevelStart();
            _readySignalAttempts++;
            LoggerInstance.Msg(
                $"Sent race-ready fallback for {localPlayer.nickName} " +
                $"(attempt {_readySignalAttempts}).");

            _readySignalAt = _readySignalAttempts >= 3
                ? -1
                : Time.unscaledTime + 1.5f;
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Race-ready fallback failed: {exception}");
            _readySignalAt = -1;
        }
    }

    private void ProbeNetworkRacerInitialization()
    {
        try
        {
            var server = FRNetworkServer.instance;
            var players = server ? server.GetPlayers() : null;
            if (players is null)
            {
                LoggerInstance.Error("Racer initialization probe has no server player registry.");
                return;
            }

            var index = 0;
            foreach (var player in players.Values)
            {
                var racer = player ? player.GetComponent<FRNetworkRacer>() : null;
                if (!racer)
                {
                    LoggerInstance.Error($"Racer initialization probe player {index} has no FRNetworkRacer.");
                    index++;
                    continue;
                }

                racer!.Initialize(index);
                LoggerInstance.Msg(
                    $"Racer initialization probe succeeded for player {index}: " +
                    $"racerIndex={racer._racerIndex} transform={DescribeComponent(racer._nett)} " +
                    $"target={DescribeComponent(racer._nett ? racer._nett.target : null)}");
                index++;
            }

            if (_syncSpawnDiagnostic)
            {
                var gameState = FRNetGameState.instance;
                if (!gameState || !gameState._gameSyncPrefab)
                {
                    LoggerInstance.Error("ServerSync spawn probe has no prefab.");
                    return;
                }

                var instance = UnityEngine.Object.Instantiate(
                    gameState._gameSyncPrefab,
                    Vector3.zero,
                    Quaternion.identity);
                gameState._gameSyncInstance = instance;
                Il2CppMirror.NetworkServer.Spawn(instance.gameObject);
                LoggerInstance.Msg(
                    $"ServerSync spawn probe succeeded: {DescribeComponent(instance)} " +
                    $"netId={instance.netId} clientPrefabs={Il2CppMirror.NetworkClient.prefabs.Count}.");

                try
                {
                    if (gameState.onGameSyncStart is not null)
                    {
                        gameState.onGameSyncStart.Invoke();
                        LoggerInstance.Msg("onGameSyncStart probe succeeded.");
                    }
                    else
                    {
                        LoggerInstance.Msg("onGameSyncStart probe has no subscribers.");
                    }
                }
                catch (Exception exception)
                {
                    LoggerInstance.Error($"onGameSyncStart probe failed: {exception}");
                }

                try
                {
                    var timestamp = Il2CppMirror.NetworkTime.time;
                    GameManager.StartSequence(timestamp);
                    LoggerInstance.Msg($"Local StartSequence probe succeeded at {timestamp:F3}.");
                }
                catch (Exception exception)
                {
                    LoggerInstance.Error($"Local StartSequence probe failed: {exception}");
                }
            }
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Racer initialization probe failed: {exception}");
        }
    }

    private void ProbeNetworkRuntime()
    {
        try
        {
            var frSingleton = FRNetworkManager.instancia;
            var mirrorSingleton = Il2CppMirror.NetworkManager.singleton;
            var managers = Resources.FindObjectsOfTypeAll<FRNetworkManager>();

            LoggerInstance.Msg(
                $"Network probe scene={_lastSceneName}: " +
                $"FR singleton={DescribeComponent(frSingleton)}; " +
                $"Mirror singleton={DescribeComponent(mirrorSingleton)}; " +
                $"FR objects={managers.Length}");

            for (var index = 0; index < managers.Length; index++)
            {
                LoggerInstance.Msg($"Network probe FR[{index}]={DescribeComponent(managers[index])}");
            }

            LoggerInstance.Msg(
                $"Network companions: client={DescribeComponent(FRNetworkClient.instance)}; " +
                $"server={DescribeComponent(FRNetworkServer.instance)}; " +
                $"gameState={DescribeComponent(FRNetGameState.instance)}; " +
                $"multiplayer={DescribeComponent(MultiplayerManager.instance)}");

            if (frSingleton)
            {
                var spawnPrefabs = frSingleton.spawnPrefabs;
                LoggerInstance.Msg(
                    $"Network prefabs: player={DescribeGameObject(frSingleton.playerPrefab)}; " +
                    $"registered={(spawnPrefabs is null ? -1 : spawnPrefabs.Count)}");
                if (spawnPrefabs is not null)
                {
                    for (var index = 0; index < spawnPrefabs.Count; index++)
                    {
                        LoggerInstance.Msg(
                            $"Network spawnPrefab[{index}]={DescribeGameObject(spawnPrefabs[index])}");
                    }
                }

                var gameState = FRNetGameState.instance;
                LoggerInstance.Msg(
                    $"Network gameSync prefab=" +
                    $"{DescribeGameObject(gameState ? gameState._gameSyncPrefab?.gameObject : null)}");
                var clientPrefabs = Il2CppMirror.NetworkClient.prefabs;
                LoggerInstance.Msg(
                    $"NetworkClient accepted prefabs=" +
                    $"{(clientPrefabs is null ? -1 : clientPrefabs.Count)}");
            }
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Network runtime probe failed: {exception}");
        }
    }

    private static string DescribeComponent(Component? component)
    {
        if (component is null || !component)
        {
            return "null/destroyed";
        }

        var gameObject = component.gameObject;
        var enabled = component is Behaviour behaviour ? behaviour.enabled : true;
        return $"{gameObject.name}@{component.Pointer} " +
               $"scene={gameObject.scene.name} activeSelf={gameObject.activeSelf} " +
               $"activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}";
    }

    private static string DescribeGameObject(GameObject? gameObject)
    {
        if (gameObject is null || !gameObject)
        {
            return "null/destroyed";
        }

        var scene = gameObject.scene;
        var identity = gameObject.GetComponent<Il2CppMirror.NetworkIdentity>();
        var networkId = identity
            ? $"assetId={identity.assetId} sceneId={identity.sceneId}"
            : "identity=missing";
        return $"{gameObject.name}@{gameObject.Pointer} scene={(scene.IsValid() ? scene.name : "prefab")} " +
               $"{networkId} " +
               $"activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}";
    }

    private bool TryApplySettings(out FRNetworkManager manager)
    {
        if (!TryInitializeMultiplayerSystem())
        {
            manager = null!;
            return false;
        }

        manager = FRNetworkManager.instancia;
        if (!manager)
        {
            _message = "FRNetworkManager is not available in this scene.";
            return false;
        }

        var nickname = _nickname.Trim();
        if (string.IsNullOrWhiteSpace(nickname) || nickname.Length >= 64)
        {
            _message = "Nickname must contain 1-63 characters.";
            return false;
        }

        var address = _address.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            _message = "Host address cannot be empty.";
            return false;
        }

        manager.networkAddress = address;

        var client = FRNetworkClient.instance;
        if (client)
        {
            client.SetNickname(nickname);
        }

        if (!EnsureNetworkPrefabs(manager))
        {
            return false;
        }

        _localInputWarning = null;
        if (!EnsureLocalGamePlayer())
        {
            var players = GameManager.players;
            var localPlayerExists = players is not null && players.Length > 0 && players[0] is not null;
            if (!localPlayerExists)
            {
                return false;
            }

            _localInputWarning = _message;
            LoggerInstance.Warning(
                $"Continuing network startup with a local input warning: {_localInputWarning}");
        }

        var gameState = FRNetGameState.instance;
        if (gameState && !string.IsNullOrWhiteSpace(_map))
        {
            gameState.map = _map.Trim();
            if (!int.TryParse(_laps, out var laps) || laps < 1 || laps > 99)
            {
                _message = "Laps must be a number from 1 to 99.";
                return false;
            }

            gameState._settings._laps = laps;
        }

        return true;
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

        RegisterSpawnPrefab(spawnPrefabs, playerPrefab!);
        RegisterSpawnPrefab(spawnPrefabs, gameSyncPrefab!);
        return true;
    }

    private void RegisterSpawnPrefab(
        Il2CppSystem.Collections.Generic.List<GameObject> spawnPrefabs,
        GameObject prefab)
    {
        for (var index = 0; index < spawnPrefabs.Count; index++)
        {
            if (spawnPrefabs[index] == prefab)
            {
                return;
            }
        }

        spawnPrefabs.Add(prefab);
        LoggerInstance.Msg($"Registered Mirror spawn prefab: {prefab.name}.");
    }

    private bool EnsureLocalGamePlayer()
    {
        var players = GameManager.players;
        if (players is null || players.Length < 1)
        {
            _message = "GameManager has no local player slots.";
            return false;
        }

        var localPlayer = players[0];
        if (Application.isBatchMode)
        {
            if (localPlayer is null)
            {
                localPlayer = GameManager.AddHuman(0);
                if (localPlayer is null)
                {
                    _message = "The game's local player could not be initialized.";
                    return false;
                }

                LoggerInstance.Msg(
                    $"Initialized the missing local GameManager player in slot 0: " +
                    $"{localPlayer.GetType().Name}@{localPlayer.Pointer}");
            }

            LoggerInstance.Msg("Batch-mode player initialized without a physical input device.");
            return true;
        }

        if (localPlayer is not null && localPlayer.input)
        {
            LoggerInstance.Msg(
                $"Using existing local PlayerInput index={localPlayer.input.playerIndex} " +
                $"scheme={localPlayer.input.currentControlScheme}.");
            return true;
        }

        return EnsureLocalPlayerInput();
    }

    private bool EnsureLocalPlayerInput()
    {
        var racingInputManager = RacingInputManager.instance;
        if (!racingInputManager || !racingInputManager.manager)
        {
            _message = "RacingInputManager is not available for the local player.";
            return false;
        }

        PlayerInput? menuInput = null;
        InputDevice? device = null;
        string? controlScheme = null;
        var menuPlayerObject = GameObject.Find("Player");
        if (menuPlayerObject)
        {
            menuInput = menuPlayerObject.GetComponent<PlayerInput>();
        }

        if (menuInput)
        {
            var activeMenuInput = menuInput!;
            var devices = activeMenuInput.devices;
            if (devices.Count > 0)
            {
                device = devices[0];
                controlScheme = activeMenuInput.currentControlScheme;
            }

        }

        // The restored menu can keep an active index-0 PlayerInput whose object is
        // not named "Player". Capture its device/scheme as a fallback, then disable
        // every old PlayerInput before joining the racing prefab. This mirrors the
        // original character-selection rearrange path and prevents duplicate index 0.
        var staleInputs = new List<PlayerInput>();
        var activeInputs = PlayerInput.all;
        for (var index = 0; index < activeInputs.Count; index++)
        {
            var candidate = activeInputs[index];
            if (!candidate)
            {
                continue;
            }

            staleInputs.Add(candidate);
            var candidateDevices = candidate.devices;
            if (device is null && candidateDevices.Count > 0)
            {
                device = candidateDevices[0];
            }

            if (candidateDevices.Count > 0 &&
                string.IsNullOrWhiteSpace(controlScheme) &&
                !string.IsNullOrWhiteSpace(candidate.currentControlScheme))
            {
                controlScheme = candidate.currentControlScheme;
            }

            LoggerInstance.Msg(
                $"Existing PlayerInput[{index}]: name={candidate.gameObject.name} " +
                $"pointer={candidate.Pointer} playerIndex={candidate.playerIndex} " +
                $"scheme={candidate.currentControlScheme ?? "<none>"} " +
                $"devices={candidateDevices.Count} active={candidate.active}.");
        }

        for (var index = 0; index < staleInputs.Count; index++)
        {
            var staleInput = staleInputs[index];
            if (!staleInput)
            {
                continue;
            }

            staleInput.DeactivateInput();
            staleInput.enabled = false;
            UnityEngine.Object.Destroy(staleInput.gameObject);
        }

        device ??= Keyboard.current;
        device ??= Gamepad.current;
        if (device is null)
        {
            _message = "No keyboard or gamepad is available for the local racer.";
            return false;
        }

        LoggerInstance.Msg(
            $"Rebinding local input from menu player: " +
            $"scheme={controlScheme ?? "<automatic>"} device={device.displayName}.");

        // AddDefaultPlayer removes every HumanGamePlayer before joining and relies on
        // RacingInputManager.OnPlayerJoined to recreate and bind slot 0. That callback
        // can stop after AddHuman in the restored multiplayer hierarchy, leaving a local
        // racer whose input is null. Keep the PlayerInput returned by AddPlayer so the
        // binding can be completed explicitly below.
        PlayerInput? joinedInput = null;
        try
        {
            joinedInput = racingInputManager.AddPlayer(
                0,
                string.IsNullOrWhiteSpace(controlScheme) ? null! : controlScheme,
                device);
        }
        catch (Exception exception)
        {
            // JoinPlayer creates the PlayerInput before invoking the restored
            // RacingInputManager callback. Recover that object when the callback
            // fails during its bookkeeping, then finish the essential binding here.
            LoggerInstance.Warning(
                $"Original local player-joined callback did not complete: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }

        if (!joinedInput)
        {
            joinedInput = FindJoinedPlayerInput();
        }

        if (!joinedInput)
        {
            _message = "The input system did not create PlayerInput for the local racer.";
            return false;
        }

        var createdInput = joinedInput!;

        var players = GameManager.players;
        var localPlayer = players is not null && players.Length > 0 ? players[0] : null;
        if (localPlayer is null)
        {
            localPlayer = GameManager.AddHuman(0);
        }

        var defaultControls = ProfilesManager.instance
            ? ProfilesManager.instance.GetDefaultControls()
            : racingInputManager.defaultControls;
        if (defaultControls)
        {
            var previousActions = createdInput.actions;
            if (previousActions && previousActions != defaultControls)
            {
                previousActions.Disable();
            }

            createdInput.actions = defaultControls;
            defaultControls.Enable();
        }

        createdInput.enabled = true;
        createdInput.ActivateInput();
        localPlayer?.SetInput(createdInput);

        var humanPlayerInput = createdInput.GetComponent<HumanPlayerInput>();
        if (humanPlayerInput && localPlayer is not null)
        {
            humanPlayerInput.Assign(localPlayer);
            humanPlayerInput.enabled = true;
        }

        createdInput.transform.SetParent(racingInputManager.transform, false);
        if (racingInputManager._humanInputIndex is not null &&
            racingInputManager._humanInputIndex.Length > 0)
        {
            racingInputManager._humanInputIndex[0] = 0;
            racingInputManager._humanCount = Math.Max(racingInputManager._humanCount, 1);
        }

        if (localPlayer is null || !localPlayer.input)
        {
            _message = "The local racer was created, but PlayerInput did not bind.";
            return false;
        }

        LoggerInstance.Msg(
            $"Bound local racer input: index={localPlayer.input.playerIndex} " +
            $"scheme={localPlayer.input.currentControlScheme} device={device.displayName} " +
            $"object={localPlayer.input.gameObject.name}@{localPlayer.input.Pointer} " +
            $"actionsEnabled={(localPlayer.input.actions ? localPlayer.input.actions.enabled : false)} " +
            $"humanInput={(humanPlayerInput ? "ready" : "missing")} " +
            $"controller={(localPlayer.input.GetComponent<PlayerRacingController>() ? "ready" : "missing")}.");
        return true;
    }

    private static PlayerInput? FindJoinedPlayerInput()
    {
        var inputs = PlayerInput.all;
        for (var index = 0; index < inputs.Count; index++)
        {
            var candidate = inputs[index];
            if (!candidate || candidate.playerIndex != 0)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private bool TryEnsureLocalDrivingController()
    {
        try
        {
            var players = GameManager.players;
            var localPlayer = players is not null && players.Length > 0 ? players[0] : null;
            var boundInput = localPlayer?.input;
            if (localPlayer is null || !boundInput)
            {
                if (_localDrivingRepairAttempts == 1)
                {
                    LoggerInstance.Warning("Waiting for the local player's bound PlayerInput.");
                }

                return false;
            }

            var activeBoundInput = boundInput!;

            var indexedInput = PlayerInput.GetPlayerByIndex(0);
            var controller = activeBoundInput.GetComponent<PlayerRacingController>();
            var humanPlayerInput = activeBoundInput.GetComponent<HumanPlayerInput>();
            var actions = activeBoundInput.actions;
            var racer = localPlayer.racer;

            if (_localDrivingRepairAttempts == 1 || racer)
            {
                LoggerInstance.Msg(
                    $"Local driving probe attempt={_localDrivingRepairAttempts}: " +
                    $"bound={activeBoundInput.gameObject.name}@{activeBoundInput.Pointer} " +
                    $"indexed={(indexedInput ? $"{indexedInput.gameObject.name}@{indexedInput.Pointer}" : "null")} " +
                    $"sameIndex={(indexedInput && indexedInput.Pointer == activeBoundInput.Pointer)} " +
                    $"inputActive={activeBoundInput.active} enabled={activeBoundInput.enabled} " +
                    $"actions={(actions ? actions.name : "null")} " +
                    $"actionsEnabled={(actions ? actions.enabled : false)} " +
                    $"actionMap={(activeBoundInput.currentActionMap is not null ? activeBoundInput.currentActionMap.name : "<all>")} " +
                    $"humanInput={(humanPlayerInput ? "ready" : "missing")} " +
                    $"controller={(controller ? $"enabled={controller.enabled}" : "missing")} " +
                    $"racer={(racer ? $"ready@{racer.Pointer}" : "waiting")}.");
            }

            activeBoundInput.enabled = true;
            activeBoundInput.ActivateInput();
            if (actions)
            {
                actions.Enable();
            }

            if (humanPlayerInput)
            {
                humanPlayerInput.Assign(localPlayer);
                humanPlayerInput.enabled = true;
            }

            if (!racer || !controller)
            {
                return false;
            }

            controller.enabled = true;
            controller.Init(localPlayer, racer);
            _message = "Local driving input is ready.";
            LoggerInstance.Msg(
                $"Local driving controller ready: playerInput={activeBoundInput.Pointer} " +
                $"controller={controller.Pointer} racer={racer.Pointer} " +
                $"scheme={activeBoundInput.currentControlScheme}.");
            return true;
        }
        catch (Exception exception)
        {
            if (_localDrivingRepairAttempts == 1 ||
                Time.unscaledTime + 0.5f >= _localDrivingRepairUntil)
            {
                LoggerInstance.Warning(
                    $"Local driving controller repair attempt {_localDrivingRepairAttempts} failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }

            return false;
        }
    }

    private void TryStartHost()
    {
        try
        {
            if (!TryApplySettings(out var manager))
            {
                return;
            }

            if (manager.mode != Il2CppMirror.NetworkManagerMode.Offline)
            {
                _message = $"Network is already {manager.mode}. Stop it before hosting again.";
                return;
            }

            manager.StartHost();
            EnsureNetworkDiagnostics();
            _message = _localInputWarning is null
                ? "Host started on UDP 7777. Add players, then press Start Race."
                : $"Host started on UDP 7777, but driving input needs attention: {_localInputWarning}";
            LoggerInstance.Msg(_message);
        }
        catch (Exception exception)
        {
            ReportFailure("Host failed", exception);
        }
    }

    private void TryStartClient()
    {
        try
        {
            if (!TryApplySettings(out var manager))
            {
                return;
            }

            if (manager.mode != Il2CppMirror.NetworkManagerMode.Offline)
            {
                _message = $"Network is already {manager.mode}. Stop it before joining.";
                return;
            }

            manager.StartClient();
            EnsureNetworkDiagnostics();
            _message = _localInputWarning is null
                ? $"Connecting to {_address.Trim()}:7777 as {_nickname.Trim()}..."
                : $"Connecting to {_address.Trim()}:7777; driving input warning: {_localInputWarning}";
            LoggerInstance.Msg(_message);
        }
        catch (Exception exception)
        {
            ReportFailure("Join failed", exception);
        }
    }

    private void EnsureNetworkDiagnostics()
    {
        if (_networkDiagnosticsHooked)
        {
            return;
        }

        _clientDisconnectedDiagnostic = (Il2CppSystem.Action)(System.Action)(() =>
        {
            LoggerInstance.Warning(
                $"Mirror client disconnected: scene={_lastSceneName} " +
                $"mode={(FRNetworkManager.instancia ? FRNetworkManager.instancia.mode.ToString() : "unavailable")}.");
        });
        _clientErrorDiagnostic =
            (Il2CppSystem.Action<Il2CppSystem.Exception>)(System.Action<Il2CppSystem.Exception>)(exception =>
            {
                LoggerInstance.Error($"Mirror client error before disconnect: {exception}");
            });

        Il2CppMirror.NetworkClient.OnDisconnectedEvent += _clientDisconnectedDiagnostic;
        Il2CppMirror.NetworkClient.OnErrorEvent += _clientErrorDiagnostic;
        _networkDiagnosticsHooked = true;
        LoggerInstance.Msg("Installed Mirror client error/disconnect diagnostics.");
    }

    private void TryStartRace()
    {
        try
        {
            var manager = FRNetworkManager.instancia;
            if (!manager || manager.mode != Il2CppMirror.NetworkManagerMode.Host)
            {
                _message = "Start Race is available only after this instance becomes Host.";
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
            if (playerCount < 1)
            {
                _message = "Waiting for the host player's network handshake.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_map) ||
                !int.TryParse(_laps, out var laps) || laps < 1 || laps > 99)
            {
                _message = "Choose a map and set laps from 1 to 99.";
                return;
            }

            var map = _map.Trim();
            if (!EnsureNetworkGameMode(map))
            {
                return;
            }

            gameState.map = map;
            gameState._settings._laps = laps;
            gameState.StartGame(gameState._settings);

            // Do not leave an IMGUI text field focused while the racing PlayerInput
            // is taking over the keyboard.
            _showPanel = false;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;

            _message = $"Starting {map} with {playerCount} player(s), {laps} lap(s)...";
            LoggerInstance.Msg(_message);
            if (_hostSmokeDiagnostic)
            {
                _raceProbeAt = Time.unscaledTime + 0.25f;
                _raceProbeUntil = Time.unscaledTime + 20.0f;
            }
        }
        catch (Exception exception)
        {
            ReportFailure("Start Race failed", exception);
        }
    }

    private bool EnsureNetworkGameMode(string map)
    {
        var gameModeManager = GameModeManager.instance;
        if (!gameModeManager)
        {
            _message = "GameModeManager is not available.";
            return false;
        }

        var current = gameModeManager.currentGameMode;
        if (current)
        {
            LoggerInstance.Msg(
                $"Using existing game mode {current.GetType().Name}; " +
                $"countdown={current.GetCountdown()}.");
            return true;
        }

        var prefabObject = ResourcesManager.Load<GameObject>("GameModes/QuickRace");
        var prefab = prefabObject ? prefabObject.GetComponent<GameMode>() : null;
        if (!prefab)
        {
            _message = "The original QuickRace game-mode prefab could not be loaded.";
            return false;
        }

        CircuitData? circuit = null;
        var circuits = GameManager._circuits;
        if (circuits is not null)
        {
            for (var index = 0; index < circuits.Count; index++)
            {
                var candidate = circuits[index];
                if (candidate is not null &&
                    candidate._scene.Equals(map, StringComparison.OrdinalIgnoreCase))
                {
                    circuit = candidate;
                    break;
                }
            }
        }

        if (circuit is null)
        {
            _message = $"No original circuit metadata matches map '{map}'.";
            return false;
        }

        var instance = UnityEngine.Object.Instantiate(prefab!, Vector3.zero, Quaternion.identity);
        var playlist = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<CircuitData>(1);
        playlist[0] = circuit;
        instance._playlist = playlist;
        instance._playlistIndex = 0;
        UnityEngine.Object.DontDestroyOnLoad(instance.gameObject);

        gameModeManager._currentGM = instance;
        _networkGameMode = instance;
        LoggerInstance.Msg(
            $"Prepared original QuickRace mode for network play: map={circuit._scene} " +
            $"countdown={instance.GetCountdown()}.");
        return true;
    }

    private void TryStopNetwork()
    {
        try
        {
            var manager = FRNetworkManager.instancia;
            if (!manager)
            {
                _message = "FRNetworkManager is not available.";
                return;
            }

            switch (manager.mode)
            {
                case Il2CppMirror.NetworkManagerMode.Host:
                    manager.StopHost();
                    break;
                case Il2CppMirror.NetworkManagerMode.ClientOnly:
                    manager.StopClient();
                    break;
                case Il2CppMirror.NetworkManagerMode.ServerOnly:
                    manager.StopServer();
                    break;
            }

            var multiplayerManager = MultiplayerManager.instance;
            if (multiplayerManager && multiplayerManager._system is not null)
            {
                multiplayerManager._system.Close();
                multiplayerManager._system = null;
            }

            CleanupNetworkGameMode();

            _message = "Network session stopped.";
            LoggerInstance.Msg(_message);
        }
        catch (Exception exception)
        {
            ReportFailure("Stop failed", exception);
        }
    }

    private void CleanupNetworkGameMode()
    {
        var networkGameMode = _networkGameMode;
        if (networkGameMode is null || !networkGameMode)
        {
            _networkGameMode = null;
            return;
        }

        var gameModeManager = GameModeManager.instance;
        if (gameModeManager && gameModeManager.currentGameMode == networkGameMode)
        {
            gameModeManager._currentGM = null;
        }

        UnityEngine.Object.Destroy(networkGameMode.gameObject);
        _networkGameMode = null;
    }

    private static string GetNetworkStatus()
    {
        try
        {
            var manager = FRNetworkManager.instancia;
            if (!manager)
            {
                return "Network mode: manager unavailable";
            }

            var server = FRNetworkServer.instance;
            return server && Il2CppMirror.NetworkServer.active
                ? $"Network mode: {manager.mode} | Players: {server.GetPlayers().Count}"
                : $"Network mode: {manager.mode}";
        }
        catch
        {
            return "Network mode: unavailable";
        }
    }

    private void ProbeRaceProgress()
    {
        try
        {
            var manager = FRNetworkManager.instancia;
            var server = FRNetworkServer.instance;
            var gameState = FRNetGameState.instance;
            var gameManager = GameManager.instance;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var serverPlayers = server ? server.GetPlayers() : null;
            var localNetworkPlayer = FRNetworkPlayer.localPlayer;
            var gamePlayers = GameManager.players;
            var racers = GameManager.racers;
            var connectionCount = gameState && gameState._connections is not null
                ? gameState._connections.Length
                : -1;

            LoggerInstance.Msg(
                $"Race probe scene={scene.name} mode={(manager ? manager.mode.ToString() : "unavailable")} " +
                $"clientActive={Il2CppMirror.NetworkClient.active} serverActive={Il2CppMirror.NetworkServer.active} " +
                $"serverPlayers={(serverPlayers is null ? -1 : serverPlayers.Count)} " +
                $"connections={connectionCount} localNetworkPlayer={DescribeNetworkPlayer(localNetworkPlayer)} " +
                $"gameManager={DescribeComponent(gameManager)} gamePlayers={(gamePlayers is null ? -1 : gamePlayers.Length)} " +
                $"racers={(racers is null ? -1 : racers.Length)} " +
                $"gameSyncPrefab={DescribeComponent(gameState ? gameState._gameSyncPrefab : null)} " +
                $"gameSyncInstance={DescribeComponent(gameState ? gameState._gameSyncInstance : null)} " +
                $"inRace={GameManager.inRace}");

            if (serverPlayers is not null)
            {
                var index = 0;
                foreach (var player in serverPlayers.Values)
                {
                    LoggerInstance.Msg($"Race probe networkPlayer[{index++}]={DescribeNetworkPlayer(player)}");
                }
            }

            if (gamePlayers is not null)
            {
                for (var index = 0; index < gamePlayers.Length; index++)
                {
                    var player = gamePlayers[index];
                    LoggerInstance.Msg(
                        player is null
                            ? $"Race probe gamePlayer[{index}]=null"
                            : $"Race probe gamePlayer[{index}]={player.GetType().Name}@{player.Pointer}");
                }
            }
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Race progress probe failed: {exception}");
        }
    }

    private static string DescribeNetworkPlayer(FRNetworkPlayer? player)
    {
        if (player is null || !player)
        {
            return "null/destroyed";
        }

        var networkRacer = player.GetComponent<FRNetworkRacer>();
        var networkRacerDescription = networkRacer
            ? $"{DescribeComponent(networkRacer)} racerIndex={networkRacer._racerIndex} " +
              $"transform={DescribeComponent(networkRacer._nett)}"
            : "null/destroyed";

        return $"{player.nickName}@{player.Pointer} local={player.isLocalPlayer} " +
               $"authority={player.hasAuthority} serverReady={player.isServerReady} netId={player.netId} " +
               $"netRacer={networkRacerDescription}";
    }

    private void ReportFailure(string operation, Exception exception)
    {
        _message = $"{operation}: {exception.GetType().Name}: {exception.Message}";
        LoggerInstance.Error(_message);
        LoggerInstance.Error(exception);
    }
}
