using Il2Cpp;
using Il2CppMirror;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TFROnlineMenu;

internal static class OnlineSelection
{
    internal const string SelectionScene = "selection";
    const int MaxSlots = 4;
    const float HostPollInterval = 0.25f;
    const float ReturnToMenuRetry = 12f;
    const float DisconnectGrace = 2f;

    static bool _entering;
    static bool _slotsReady;
    static bool _startingRace;
    static bool _leaving;
    static bool _hadSession;
    static bool _followSelection;
    static bool _returningToMenu;
    static bool _localConfirmed;
    static int _lastCount;
    static float _nextHostPoll;
    static float _returnToMenuAt;
    static float _offlineAt = -1;
    static readonly Dictionary<uint, (int Character, int Skin, int Vehicle)> LastRemotePicks = new();
    static readonly HashSet<uint> ConfirmedPeers = new();

    internal static bool IsActive { get; private set; }

    internal static int LocalSlot
    {
        get
        {
            var local = FRNetworkPlayer.localPlayer;
            return local ? SlotOf(local) : 0;
        }
    }

    internal static bool IsOnlineSession =>
        NetworkServer.active || NetworkClient.active;

    internal static bool ShouldStayInSession
    {
        get
        {
            if (_leaving || !IsOnlineSession)
            {
                return false;
            }

            if (IsActive || _entering || _followSelection)
            {
                return true;
            }

            return IsLobbyScene(SceneManager.GetActiveScene().name);
        }
    }

    internal static void BeginFromHost()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        var manager = FRNetworkManager.instancia;
        if (!manager)
        {
            OnlineMenuMod.Instance.Message = "FRNetworkManager is not available.";
            return;
        }

        IsActive = true;
        _followSelection = true;
        PublishHostLobbyState(true);
        EnterSelectionLocally();
        OnlineMenuMod.Instance.Message = "Starting online Quick Race selection...";
    }

    internal static void NotifyClientConnected()
    {
        _hadSession = true;
        _leaving = false;
        _offlineAt = -1;
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
        if (NetworkServer.active || !NetworkClient.active)
        {
            return;
        }

        TryFollowHost(force: true);
    }

    internal static void FinishFromMenu(SelectionMenuBehaviour menu)
    {
        if (!NetworkServer.active || _startingRace)
        {
            return;
        }

        var netPlayers = GetNetworkPlayers();
        if (netPlayers.Count < 2)
        {
            OnlineMenuMod.Instance.Message = $"At least two players are required (found {netPlayers.Count}).";
            return;
        }

        if (!AllPlayersConfirmed(out var confirmed, out var total))
        {
            OnlineMenuMod.Instance.Message =
                $"Wait for every player to confirm character and kart ({confirmed}/{total}).";
            MelonLogger.Msg($"[Online] Blocked map start: {confirmed}/{total} player(s) confirmed.");
            return;
        }

        if (menu && menu._currentModule is not null)
        {
            menu._currentModule.End();
        }

        _startingRace = true;
        var props = new GameModeManager.SGameModeProperties
        {
            _cup = menu._cup,
            _maps = menu._maps,
            _itemType = 0,
            _laps = menu._laps,
            _maxPlayers = netPlayers.Count,
            _humans = GameManager.players
        };
        MelonLogger.Msg($"[Online] Host confirmed map. Players={netPlayers.Count}, maps={(props._maps is null ? 0 : props._maps.Length)}.");
        OnlineMenuMod.Instance.StartRaceFromSelection(props);
        _startingRace = false;
    }

    internal static void EnterSelectionLocally()
    {
        if (_entering)
        {
            return;
        }

        if (FindSelectionMenu() is not null)
        {
            IsActive = true;
            _entering = false;
            return;
        }

        var menu = MainMenuManager.instance;
        var levels = LevelManager.instance;
        if (!menu || !levels)
        {
            return;
        }

        _entering = true;
        IsActive = true;
        _followSelection = true;
        _slotsReady = false;
        _lastCount = 0;
        _localConfirmed = false;
        LastRemotePicks.Clear();
        InstallSelectionSequence(menu);

        if (SceneManager.GetActiveScene().name.Equals(SelectionScene, StringComparison.OrdinalIgnoreCase))
        {
            _entering = false;
            menu.OnSceneLoaded();
            return;
        }

        menu.GoToSelection(menu._startModule, null);
    }

    internal static void InstallSelectionSequence(MainMenuManager? menu = null)
    {
        menu ??= MainMenuManager.instance;
        if (!menu || menu._startModule is not null)
        {
            return;
        }

        var characters = new CharacterSelectionModule("characters", MaxSlots);
        var cups = new CupSelectionModule("cups", true);
        characters.next = cups;
        cups.prev = characters;
        menu._startModule = characters;
    }

    internal static void HandleSceneInitialized(string sceneName)
    {
        if (sceneName.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
            sceneName.Equals("mainscene", StringComparison.OrdinalIgnoreCase))
        {
            _entering = false;
            _slotsReady = false;
            _returningToMenu = false;
            return;
        }

        if (!sceneName.Equals(SelectionScene, StringComparison.OrdinalIgnoreCase))
        {
            if (!sceneName.Equals("loading", StringComparison.OrdinalIgnoreCase))
            {
                _entering = false;
                _slotsReady = false;
                if (IsActive)
                {
                    NotifyRaceStarting();
                }
            }

            return;
        }

        if (!IsOnlineSession)
        {
            return;
        }

        IsActive = true;
        _followSelection = true;
        _entering = false;
        EnsureClientReady();
    }

    internal static void HandleSelectionUiReady()
    {
        if (!IsOnlineSession)
        {
            return;
        }

        IsActive = true;
        _followSelection = true;
        _entering = false;
        EnsureClientReady();
        ApplySlots();
    }

    internal static void Tick()
    {
        WatchDisconnect();
        WatchReturnToMenu();
        if (NetworkServer.active && _followSelection && IsActive && !GameManager.inRace)
        {
            PublishHostLobbyState(true);
        }

        TryFollowHost(force: false);
        TFROnlineMenu.Patches.RacerInfoSync.PushLocal();
        RaceProgress.Tick();
        if (!IsActive || !IsOnlineSession)
        {
            return;
        }

        PrunePeers();
        SyncLocalConfirmed();

        if (!FindSelectionMenu())
        {
            return;
        }

        var count = GetNetworkPlayers().Count;
        if (!_slotsReady || count != _lastCount)
        {
            try
            {
                ApplySlots();
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"[Online] ApplySlots: {exception.Message}");
                _slotsReady = true;
                _lastCount = Math.Clamp(count, 1, MaxSlots);
            }
        }

        ApplyAllRemotePicks();
    }

    static void WatchDisconnect()
    {
        var online = NetworkServer.active || NetworkClient.active;
        if (online)
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

        if (Time.unscaledTime - _offlineAt < DisconnectGrace)
        {
            return;
        }

        HandleDisconnect();
    }

    static void WatchReturnToMenu()
    {
        if (!_returningToMenu)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene().name;
        if (scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
            scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase))
        {
            _returningToMenu = false;
            return;
        }

        if (IsLoadingScene(scene))
        {
            return;
        }

        if (Time.unscaledTime < _returnToMenuAt)
        {
            return;
        }

        _returningToMenu = false;
        if (LevelManager.instance)
        {
            MelonLogger.Msg("[Online] Disconnect left a non-menu scene; loading the main menu once.");
            LevelManager.instance.LoadMainMenu();
        }
    }

    static bool IsLoadingScene(string scene)
    {
        if (scene.Equals("loading", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var levels = LevelManager.instance;
        return levels && levels.isLoading;
    }

    static bool IsLobbyScene(string scene)
    {
        return scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("splash", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("loading", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals(SelectionScene, StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("render", StringComparison.OrdinalIgnoreCase);
    }

    static void TryFollowHost(bool force)
    {
        if (NetworkServer.active || !NetworkClient.active || _leaving || GameManager.inRace)
        {
            return;
        }

        // Once following, run every frame; otherwise poll the host's beacon at a modest rate so an idle
        // client sitting in the menu is not scanning the scene every frame.
        if (!force && !_followSelection && Time.unscaledTime < _nextHostPoll)
        {
            return;
        }

        _nextHostPoll = Time.unscaledTime + HostPollInterval;

        if (FindSelectionMenu() is not null)
        {
            IsActive = true;
            _followSelection = true;
            _entering = false;
            return;
        }

        if (!_followSelection && !HostSignalsLobby())
        {
            if (force)
            {
                OnlineMenuMod.Instance.Message = "Connected. Waiting for the host to start Quick Race.";
            }

            return;
        }

        if (force)
        {
            _entering = false;
        }

        var wasFollowing = _followSelection;
        EnterSelectionLocally();
        if (!wasFollowing && _followSelection)
        {
            MelonLogger.Msg("[Online] Host is in the selection lobby; following.");
        }
    }

    /// <summary>
    /// Advertises whether the host is sitting in the online selection lobby by tagging its own
    /// <see cref="FRNetworkPlayer.Network_info"/>. That is a Mirror SyncVar, so every client receives it
    /// through the normal spawn/delta path and can simply poll it in <see cref="HostSignalsLobby"/>.
    /// </summary>
    static void PublishHostLobbyState(bool inLobby)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        var local = FRNetworkPlayer.localPlayer;
        if (!local)
        {
            return;
        }

        var info = local.Network_info;
        var racer = info._racerInfo;
        TFROnlineMenu.Patches.RacerInfoSync.DecodePick(racer._character, racer._skin, racer._vehicle,
            out var character, out _, out _, out var ready);
        var encoded = TFROnlineMenu.Patches.RacerInfoSync.EncodeCharacter(character, ready, inLobby);
        if (racer._character == encoded)
        {
            return;
        }

        racer._character = encoded;
        info._racerInfo = racer;
        local.Network_info = info;
    }

    static bool HostSignalsLobby()
    {
        var local = FRNetworkPlayer.localPlayer;
        foreach (var player in GetNetworkPlayers())
        {
            if (!player || player == local)
            {
                continue;
            }

            if (TFROnlineMenu.Patches.RacerInfoSync.DecodeLobby(player.Network_info._racerInfo._character))
            {
                return true;
            }
        }

        return false;
    }

    internal static void NotifyRaceStarting()
    {
        PublishHostLobbyState(false);
        RaceProgress.BeginRace();
        IsActive = false;
        _followSelection = false;
        _entering = false;
    }

    internal static void BeginRace(string _)
    {
        NotifyRaceStarting();
    }

    internal static void MarkPeerConfirmed(uint netId, bool confirmed)
    {
        if (netId == 0)
        {
            return;
        }

        if (confirmed)
        {
            ConfirmedPeers.Add(netId);
        }
        else
        {
            ConfirmedPeers.Remove(netId);
        }
    }

    internal static bool AllPlayersConfirmed(out int confirmed, out int total)
    {
        PrunePeers();
        var players = GetNetworkPlayers();
        total = players.Count;
        confirmed = 0;
        foreach (var player in players)
        {
            var info = player.Network_info._racerInfo;
            TFROnlineMenu.Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
                out _, out _, out _, out var ready);
            if (ready || ConfirmedPeers.Contains(player.netId))
            {
                confirmed++;
            }
        }

        return total >= 2 && confirmed >= total;
    }

    static bool LocalConfirmed()
    {
        var behaviour = UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var slot = LocalSlot;
        return boxes is not null && slot >= 0 && slot < boxes.Length && boxes[slot] && boxes[slot].ready;
    }

    /// <summary>
    /// The character module is the only place the local player can toggle ready, and its UI is torn down once
    /// the cup module takes over. Latch the last value instead of reading a dead UI, otherwise the local
    /// player un-confirms itself the moment it advances to map selection.
    /// </summary>
    internal static bool RefreshLocalConfirmed()
    {
        if (UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>())
        {
            _localConfirmed = LocalConfirmed();
        }

        return _localConfirmed;
    }

    static void SyncLocalConfirmed()
    {
        var local = FRNetworkPlayer.localPlayer;
        if (local)
        {
            MarkPeerConfirmed(local.netId, RefreshLocalConfirmed());
        }
    }

    static void EnsureClientReady()
    {
        try
        {
            if (NetworkClient.active)
            {
                NetworkClient.Ready();
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"[Online] NetworkClient.Ready: {exception.Message}");
        }
    }

    static SelectionMenuBehaviour? FindSelectionMenu()
    {
        var menu = UnityEngine.Object.FindObjectOfType<SelectionMenuBehaviour>();
        return menu && menu._currentModule is not null ? menu : null;
    }

    static void PrunePeers()
    {
        var live = new HashSet<uint>();
        foreach (var player in GetNetworkPlayers())
        {
            live.Add(player.netId);
        }

        ConfirmedPeers.RemoveWhere(id => !live.Contains(id));
    }

    internal static void LeaveSession(string reason)
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _followSelection = false;
        _returningToMenu = true;
        _returnToMenuAt = Time.unscaledTime + ReturnToMenuRetry;
        Stop();
        var scene = SceneManager.GetActiveScene().name;
        var stayOnMenu = scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
                         scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (IsOnlineSession)
            {
                OnlineMenuMod.Instance.StopNetwork();
            }
        }
        finally
        {
            if (!stayOnMenu && LevelManager.instance)
            {
                LevelManager.instance.LoadMainMenu();
            }

            OnlineMenuMod.Instance.Message = reason;
            _leaving = false;
        }
    }

    internal static void HandleDisconnect()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _followSelection = false;
        _returningToMenu = true;
        _returnToMenuAt = Time.unscaledTime + ReturnToMenuRetry;
        Stop();
        var scene = SceneManager.GetActiveScene().name;
        if (!scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) &&
            !scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase) &&
            LevelManager.instance)
        {
            LevelManager.instance.LoadMainMenu();
        }

        OnlineMenuMod.Instance.Message = "Disconnected from host.";
        _leaving = false;
    }

    internal static void Stop()
    {
        IsActive = false;
        _entering = false;
        _startingRace = false;
        _slotsReady = false;
        _followSelection = false;
        _lastCount = 0;
        _nextHostPoll = 0;
        _hadSession = false;
        _localConfirmed = false;
        LastRemotePicks.Clear();
        ConfirmedPeers.Clear();
        _offlineAt = -1;
        TFROnlineMenu.Patches.RacerInfoSync.Reset();
        RaceProgress.Stop();
    }

    internal static void ApplyRemotePick(FRNetworkPlayer? player)
    {
        if (!player || !IsActive)
        {
            return;
        }

        var info = player!.Network_info._racerInfo;
        TFROnlineMenu.Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
            out var character, out var skin, out var vehicle, out var ready);
        // The local machine is authoritative for its own readiness; do not let the replicated copy,
        // which lags a round trip behind, overwrite the latch in RefreshLocalConfirmed.
        if (player != FRNetworkPlayer.localPlayer && (ready || character >= 0))
        {
            MarkPeerConfirmed(player.netId, ready);
        }

        ApplyRemotePick(player, character, skin, vehicle, ready);
    }

    internal static void ApplyRemotePick(FRNetworkPlayer player, int character, int skin, int vehicle, bool ready = false)
    {
        if (!player || !IsActive)
        {
            return;
        }

        var slot = SlotOf(player);
        if (slot < 0 || slot == LocalSlot)
        {
            return;
        }

        var humans = GameManager.players;
        var human = humans is not null && slot < humans.Length ? humans[slot] : null;
        if (human is null)
        {
            return;
        }

        if (ready)
        {
            MarkPeerConfirmed(player.netId, true);
        }

        var pick = (character, skin, vehicle);
        var behaviour = UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var box = boxes is not null && slot < boxes.Length ? boxes[slot] : null;
        if (LastRemotePicks.TryGetValue(player.netId, out var previous) && previous == pick)
        {
            if (box && box.ready != ready)
            {
                box._ready = ready;
            }

            return;
        }

        LastRemotePicks[player.netId] = pick;
        if (!behaviour)
        {
            human.character = character;
            human.skin = skin;
            human.vehicle = vehicle;
            return;
        }

        behaviour.RefreshMatching(slot, human, character, skin, vehicle);
        if (box)
        {
            box._ready = ready;
        }
    }

    internal static List<FRNetworkPlayer> GetNetworkPlayers()
    {
        var result = new List<FRNetworkPlayer>();
        var seen = new HashSet<uint>();
        var server = FRNetworkServer.instance;
        if (server)
        {
            var players = server.GetPlayers();
            if (players is not null)
            {
                foreach (var pair in players)
                {
                    var player = pair.Value;
                    if (player && seen.Add(player.netId))
                    {
                        result.Add(player);
                    }
                }
            }
        }

        var found = UnityEngine.Object.FindObjectsOfType<FRNetworkPlayer>(true);
        if (found is not null)
        {
            foreach (var player in found)
            {
                if (player && seen.Add(player.netId))
                {
                    result.Add(player);
                }
            }
        }

        result.Sort(static (left, right) =>
        {
            var leftHost = NetworkServer.active && left == FRNetworkPlayer.localPlayer;
            var rightHost = NetworkServer.active && right == FRNetworkPlayer.localPlayer;
            if (leftHost != rightHost)
            {
                return leftHost ? -1 : 1;
            }

            return left.netId.CompareTo(right.netId);
        });
        return result;
    }

    internal static int ConnectedCount => GetNetworkPlayers().Count;

    static int SlotOf(FRNetworkPlayer player)
    {
        if (!player)
        {
            return -1;
        }

        var index = 0;
        foreach (var candidate in GetNetworkPlayers())
        {
            if (candidate == player)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    static void ApplySlots()
    {
        var behaviour = UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>();
        if (!behaviour)
        {
            return;
        }

        var netPlayers = GetNetworkPlayers();
        var local = FRNetworkPlayer.localPlayer;
        for (var index = 0; index < MaxSlots; index++)
        {
            if (index < netPlayers.Count)
            {
                var human = GameManager.AddHuman(index);
                if (human is null)
                {
                    continue;
                }

                human._joined = true;
                SanitizeHumanPick(human);
                var netPlayer = netPlayers[index];
                if (!local || netPlayer != local)
                {
                    var info = netPlayer.Network_info._racerInfo;
                    TFROnlineMenu.Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
                        out var character, out var skin, out var vehicle, out _);
                    human.character = character < 0 ? 0 : character;
                    human.skin = Math.Max(skin, 0);
                    human.vehicle = Math.Max(vehicle, 0);
                }
            }
            else if (GameManager.players is not null && index < GameManager.players.Length &&
                     GameManager.players[index] is not null)
            {
                GameManager.players[index]._joined = false;
            }
        }

        OnlineMenuMod.Instance.EnsureSelectionInput(LocalSlot);
        SanitizeAllJoinedPicks();
        if (!behaviour._inited)
        {
            try
            {
                behaviour.Loaded(MaxSlots);
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"[Online] CharacterSelectionBehaviour.Loaded: {exception.Message}");
                RefreshJoinedBoxes(behaviour);
            }
        }
        else
        {
            RefreshJoinedBoxes(behaviour);
        }

        HideRemoteSelectors();
        _slotsReady = true;
        _lastCount = Math.Clamp(netPlayers.Count, 1, MaxSlots);
        LastRemotePicks.Clear();
        ApplyAllRemotePicks();
    }

    static void SanitizeAllJoinedPicks()
    {
        var humans = GameManager.players;
        if (humans is null)
        {
            return;
        }

        for (var index = 0; index < humans.Length; index++)
        {
            SanitizeHumanPick(humans[index]);
        }
    }

    static void SanitizeHumanPick(HumanGamePlayer? human)
    {
        if (human is null)
        {
            return;
        }

        TFROnlineMenu.Patches.RacerInfoSync.DecodePick(human.character, human.skin, human.vehicle,
            out var character, out var skin, out var vehicle, out _);
        human.character = character < 0 ? 0 : character;
        human.skin = Math.Max(skin, 0);
        human.vehicle = Math.Max(vehicle, 0);
    }

    static void RefreshJoinedBoxes(CharacterSelectionBehaviour behaviour)
    {
        var humans = GameManager.players;
        var boxes = behaviour._boxes;
        if (humans is null || boxes is null)
        {
            return;
        }

        var count = Math.Min(humans.Length, boxes.Length);
        for (var index = 0; index < count; index++)
        {
            var human = humans[index];
            var box = boxes[index];
            if (!box)
            {
                continue;
            }

            if (human is not null && human._joined)
            {
                box.Join();
                behaviour.RefreshMatching(index, human, human.character, human.skin, human.vehicle);
            }
        }
    }

    static void HideRemoteSelectors()
    {
        var behaviour = UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>();
        if (!behaviour || behaviour._selectors is null)
        {
            return;
        }

        for (var index = 0; index < behaviour._selectors.Length; index++)
        {
            var selector = behaviour._selectors[index];
            if (!selector)
            {
                continue;
            }

            selector.gameObject.SetActive(index == LocalSlot);
        }
    }

    static void ApplyAllRemotePicks()
    {
        foreach (var player in GetNetworkPlayers())
        {
            ApplyRemotePick(player);
        }
    }
}
