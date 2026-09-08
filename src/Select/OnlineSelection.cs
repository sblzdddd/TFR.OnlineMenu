using Il2Cpp;
using Il2CppMirror;
using MelonLoader;
using TFROnlineMenu.Race;
using TFROnlineMenu.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TFROnlineMenu.Select;

internal static class OnlineSelection
{
    internal const string SelectionScene = SelectionScenes.Selection;

    private const float HostPollInterval = 0.25f;
    private const float ReturnToMenuRetry = 12f;
    private const float DisconnectGrace = 2f;

    private static bool _entering;
    private static bool _startingRace;
    private static bool _followSelection;
    private static bool _leaving;
    private static bool _hadSession;
    private static bool _returningToMenu;
    private static float _nextHostPoll;
    private static float _returnToMenuAt;
    private static float _offlineAt = -1;

    private static MelonLogger.Instance Log => OnlineMenuMod.Instance.LoggerInstance;

    internal static bool IsActive { get; private set; }

    internal static bool IsOnlineSession =>
        NetworkServer.active || NetworkClient.active;

    internal static bool ShouldStayInSession =>
        !_leaving && IsOnlineSession &&
        (IsActive || _entering || _followSelection || SelectionScenes.IsLobby(SceneManager.GetActiveScene().name));

    internal static void BeginFromHost()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        IsActive = true;
        _followSelection = true;
        PlayerStatusSync.SetLobbyAdvertised(true);
        EnterSelectionLocally();
        Log.Msg("Starting online Quick Race selection...");
    }

    internal static void NotifyClientConnected()
    {
        _hadSession = true;
        _leaving = false;
        _offlineAt = -1;
        EnsureClientReady();
        Log.Msg("[Online] client session started; waiting for lobby beacon.");
    }

    internal static void OnSelectionInvite()
    {
        if (_leaving || GameManager.inRace)
        {
            return;
        }

        _followSelection = true;
        EnsureClientReady();
        EnterSelectionLocally();
    }

    internal static void RequestFollow()
    {
        if (!NetworkServer.active && NetworkClient.active)
        {
            TryFollowHost(force: true);
        }
    }

    internal static void FinishFromMenu(SelectionMenuBehaviour menu)
    {
        if (!NetworkServer.active || _startingRace)
        {
            return;
        }

        var netPlayers = SelectionRoster.GetPlayers();
        if (netPlayers.Count < 2)
        {
            Log.Error($"At least two players are required (found {netPlayers.Count}).");
            return;
        }

        if (!SelectionReady.AllConfirmed(out var confirmed, out var total))
        {
            Log.Error($"Wait for every player to confirm character and kart ({confirmed}/{total}).");
            return;
        }

        _startingRace = true;
        if (menu && menu._currentModule is not null)
        {
            menu._currentModule.End();
        }

        RaceSession.StartRaceFromSelection(new GameModeManager.SGameModeProperties
        {
            _cup = menu._cup,
            _maps = menu._maps,
            _itemType = 0,
            _laps = menu._laps,
            _maxPlayers = netPlayers.Count,
            _humans = GameManager.players
        });
        Log.Msg($"Host confirmed map. Players={netPlayers.Count}.");
        _startingRace = false;
    }

    internal static void HandleSceneInitialized(string sceneName)
    {
        if (SelectionScenes.IsMainMenu(sceneName))
        {
            _entering = false;
            SelectionSlots.Reset();
            _returningToMenu = false;
            return;
        }

        if (SelectionScenes.IsSelection(sceneName))
        {
            if (IsOnlineSession)
            {
                ArriveInSelection();
                EnsureClientReady();
            }

            return;
        }

        if (SelectionScenes.IsTransition(sceneName))
        {
            return;
        }

        _entering = false;
        SelectionSlots.Reset();
        if (IsActive)
        {
            NotifyRaceStarting();
        }
    }

    internal static void HandleSelectionUiReady()
    {
        if (!IsOnlineSession)
        {
            return;
        }

        ArriveInSelection();
        EnsureClientReady();
        SelectionSlots.Apply();
    }

    internal static void Tick()
    {
        WatchDisconnect();
        WatchReturnToMenu();
        EnsureClientReady();

        if (NetworkServer.active && _followSelection && IsActive && !GameManager.inRace)
        {
            PlayerStatusSync.SetLobbyAdvertised(true);
        }

        TryFollowHost(force: false);
        PlayerStatusSync.PushLocal();
        RaceProgress.Tick();
        if (!IsActive || !IsOnlineSession)
        {
            return;
        }

        SelectionReady.Prune();
        SelectionReady.SyncLocal();
        SelectionSlots.Tick();
    }

    internal static void NotifyRaceStarting()
    {
        PlayerStatusSync.SetLobbyAdvertised(false);
        RaceProgress.BeginRace();
        IsActive = false;
        _followSelection = false;
        _entering = false;
    }

    internal static void BeginRace(string _) => NotifyRaceStarting();

    internal static void LeaveSession(string reason) => EndSession(reason, stopNetwork: true);

    internal static void HandleDisconnect() => EndSession("Disconnected from host.", stopNetwork: false);

    internal static void Stop()
    {
        IsActive = false;
        _entering = false;
        _startingRace = false;
        _followSelection = false;
        _nextHostPoll = 0;
        _hadSession = false;
        _offlineAt = -1;
        SelectionSlots.Reset();
        SelectionReady.Reset();
        PlayerStatusSync.Reset();
        RaceProgress.Stop();
    }

    private static void EnterSelectionLocally()
    {
        if (_entering)
        {
            return;
        }

        if (SelectionSlots.FindMenu() is not null)
        {
            ArriveInSelection();
            return;
        }

        var menu = MainMenuManager.instance;
        if (!menu)
        {
            return;
        }

        // Stay true until ArriveInSelection so Tick cannot call GoToSelection again
        // while LevelManager is still on the loading scene.
        _entering = true;
        IsActive = true;
        _followSelection = true;
        SelectionSlots.Reset();
        SelectionReady.ClearLocal();
        SelectionSlots.InstallSequence(menu);

        if (SelectionScenes.IsSelection(SceneManager.GetActiveScene().name))
        {
            ArriveInSelection();
            menu.OnSceneLoaded();
            return;
        }

        Log.Msg("[Online] Loading selection scene...");
        menu.GoToSelection(menu._startModule, null);
    }

    private static void TryFollowHost(bool force)
    {
        if (NetworkServer.active || !NetworkClient.active || _leaving || GameManager.inRace)
        {
            return;
        }

        if (!force && Time.unscaledTime < _nextHostPoll)
        {
            return;
        }

        _nextHostPoll = Time.unscaledTime + HostPollInterval;
        if (SelectionSlots.FindMenu() is not null)
        {
            ArriveInSelection();
            return;
        }

        if (_entering)
        {
            return;
        }

        if (!_followSelection && !PlayerStatusSync.AnyInLobby())
        {
            return;
        }

        var wasFollowing = _followSelection;
        EnterSelectionLocally();
        if (!wasFollowing && _followSelection)
        {
            Log.Msg("[Online] Host is in the selection lobby; following.");
        }
    }

    private static void ArriveInSelection()
    {
        IsActive = true;
        _followSelection = true;
        _entering = false;
    }

    private static void WatchDisconnect()
    {
        if (IsOnlineSession)
        {
            _hadSession = true;
            _offlineAt = -1;
            return;
        }

        if (!_hadSession || _leaving)
        {
            return;
        }

        if (_offlineAt < 0)
        {
            _offlineAt = Time.unscaledTime;
        }

        if (Time.unscaledTime - _offlineAt >= DisconnectGrace)
        {
            HandleDisconnect();
        }
    }

    private static void WatchReturnToMenu()
    {
        if (!_returningToMenu)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene().name;
        if (SelectionScenes.IsMainMenu(scene))
        {
            _returningToMenu = false;
            return;
        }

        if (SelectionScenes.IsLoading(scene) || Time.unscaledTime < _returnToMenuAt)
        {
            return;
        }

        _returningToMenu = false;
        if (LevelManager.instance)
        {
            Log.Msg("[Online] Disconnect left a non-menu scene; loading the main menu once.");
            LevelManager.instance.LoadMainMenu();
        }
    }

    private static void EndSession(string reason, bool stopNetwork)
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _returningToMenu = true;
        _returnToMenuAt = Time.unscaledTime + ReturnToMenuRetry;
        Stop();

        if (stopNetwork && IsOnlineSession)
        {
            OnlineMenuMod.Instance.StopNetwork();
        }

        var scene = SceneManager.GetActiveScene().name;
        if (!SelectionScenes.IsMainMenu(scene) && LevelManager.instance)
        {
            LevelManager.instance.LoadMainMenu();
        }

        Log.Msg(reason);
        _leaving = false;
    }

    private static void EnsureClientReady()
    {
        try
        {
            if (NetworkClient.active && NetworkClient.isConnected && !NetworkClient.ready)
            {
                NetworkClient.Ready();
            }
        }
        catch (Exception exception)
        {
            Log.Warning($"[Online] NetworkClient.Ready: {exception.Message}");
        }
    }
}
