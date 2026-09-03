using Il2Cpp;
using Il2CppMirror;
using MelonLoader;
using TFROnlineMenu.Race;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Object;

namespace TFROnlineMenu.Select;

internal static class OnlineSelection
{
    internal const string SelectionScene = "selection";
    private const int MaxSlots = 4;
    private const float HostPollInterval = 0.25f;
    private const float ReturnToMenuRetry = 12f;
    private const float DisconnectGrace = 2f;

    private static bool _entering;
    private static bool _slotsReady;
    private static bool _startingRace;
    private static bool _leaving;
    private static bool _hadSession;
    private static bool _followSelection;
    private static bool _returningToMenu;
    private static bool _localConfirmed;
    private static int _lastCount;
    private static float _nextHostPoll;
    private static float _returnToMenuAt;
    private static float _offlineAt = -1;
    private static readonly Dictionary<uint, (int Character, int Skin, int Vehicle)> LastRemotePicks = new();
    private static readonly HashSet<uint> ConfirmedPeers = new();
    private static MelonLogger.Instance LoggerInstance => OnlineMenuMod.Instance.LoggerInstance;

    internal static bool IsActive { get; private set; }

    private static readonly string[] SlotUIMap =
    {
        "CharacterSlot (6)", "CharacterSlot (15)", "CharacterSlot (32)", "CharacterSlot (10)", "CharacterSlot (2)",
        "CharacterSlot (31)", "CharacterSlot (1)", "CharacterSlot (27)", "CharacterSlot (30)", "CharacterSlot (17)",
        "CharacterSlot (16)", "CharacterSlot", "CharacterSlot (12)", "CharacterSlot (26)", "CharacterSlot (28)",
        "CharacterSlot (3)", "CharacterSlot (5)", "CharacterSlot (18)", "CharacterSlot (7)", "CharacterSlot (11)",
        "CharacterSlot (29)", "CharacterSlot (8)", "CharacterSlot (13)", "CharacterSlot (9)",
    };
    internal static int LocalSlot
    {
        get
        {
            var local = FRNetworkPlayer.localPlayer;
            return local ? SlotOf(local) : -1;
        }
    }

    internal static bool IsOnlineSession =>
        NetworkServer.active || NetworkClient.active;

    internal static bool ShouldStayInSession =>
        !_leaving && IsOnlineSession &&
        (IsActive || _entering || _followSelection || IsLobbyScene(SceneManager.GetActiveScene().name));

    internal static void BeginFromHost()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        IsActive = true;
        _followSelection = true;
        PublishHostLobbyState(true);
        EnterSelectionLocally();
        LoggerInstance.Msg("Starting online Quick Race selection...");
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
            LoggerInstance.Error($"At least two players are required (found {netPlayers.Count}).");
            return;
        }

        if (!AllPlayersConfirmed(out var confirmed, out var total))
        {
            LoggerInstance.Error(
                $"Wait for every player to confirm character and kart ({confirmed}/{total})."
            );
            LoggerInstance.Error($"[Online] Blocked map start: {confirmed}/{total} player(s) confirmed.");
            return;
        }

        _startingRace = true;
        if (menu && menu._currentModule is not null)
        {
            menu._currentModule.End();
        }

        var props = new GameModeManager.SGameModeProperties
        {
            _cup = menu._cup,
            _maps = menu._maps,
            _itemType = 0,
            _laps = menu._laps,
            _maxPlayers = netPlayers.Count,
            _humans = GameManager.players
        };
        LoggerInstance.Msg($"Host confirmed map. Players={netPlayers.Count}, maps={(props._maps is null ? 0 : props._maps.Length)}.");
        RaceSession.StartRaceFromSelection(props);
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
        if (!menu)
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
        if (IsMainMenu(sceneName))
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
        Patches.RacerInfoSync.PushLocal();
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

        var players = GetNetworkPlayers();
        var count = Math.Min(players.Count, MaxSlots);
        if (!_slotsReady || count != _lastCount)
        {
            try
            {
                ApplySlots(players);
            }
            catch (Exception exception)
            {
                LoggerInstance.Warning($"[Online] ApplySlots: {exception.Message}");
                _slotsReady = true;
                _lastCount = count;
            }
        }
        else
        {
            ApplyAllRemotePicks(players);
        }
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

        if (Time.unscaledTime - _offlineAt < DisconnectGrace)
        {
            return;
        }

        HandleDisconnect();
    }

    private static void WatchReturnToMenu()
    {
        if (!_returningToMenu)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene().name;
        if (IsMainMenu(scene))
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
            LoggerInstance.Msg("[Online] Disconnect left a non-menu scene; loading the main menu once.");
            LevelManager.instance.LoadMainMenu();
        }
    }

    private static bool IsMainMenu(string scene) =>
        scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
        scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoadingScene(string scene) =>
        scene.Equals("loading", StringComparison.OrdinalIgnoreCase) ||
        LevelManager.instance && LevelManager.instance.isLoading;

    private static bool IsLobbyScene(string scene)
    {
        return IsMainMenu(scene) ||
               scene.Equals("splash", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("loading", StringComparison.OrdinalIgnoreCase) ||
               scene.Equals(SelectionScene, StringComparison.OrdinalIgnoreCase) ||
               scene.Equals("render", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryFollowHost(bool force)
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
                LoggerInstance.Msg("Connected. Waiting for the host to start Quick Race.");
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
            LoggerInstance.Msg("[Online] Host is in the selection lobby; following.");
        }
    }

    /// <summary>
    /// Advertises whether the host is sitting in the online selection lobby by tagging its own
    /// <see cref="FRNetworkPlayer.Network_info"/>. That is a Mirror SyncVar, so every client receives it
    /// through the normal spawn/delta path and can simply poll it in <see cref="HostSignalsLobby"/>.
    /// </summary>
    private static void PublishHostLobbyState(bool inLobby)
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
        Patches.RacerInfoSync.DecodePick(racer._character, racer._skin, racer._vehicle,
            out var character, out _, out _, out var ready);
        var encoded = Patches.RacerInfoSync.EncodeCharacter(character, ready, inLobby);
        if (racer._character == encoded)
        {
            return;
        }

        racer._character = encoded;
        info._racerInfo = racer;
        local.Network_info = info;
    }

    private static bool HostSignalsLobby()
    {
        // Mirror assigns netIds in spawn order, so the host owns the first player identity.
        var players = GetNetworkPlayers();
        return players.Count > 0 &&
               Patches.RacerInfoSync.DecodeLobby(
                   players[0].Network_info._racerInfo._character);
    }

    internal static void NotifyRaceStarting()
    {
        PublishHostLobbyState(false);
        RaceProgress.BeginRace();
        IsActive = false;
        _followSelection = false;
        _entering = false;
    }

    internal static void BeginRace(string _) => NotifyRaceStarting();

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
            Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
                out _, out _, out _, out var ready);
            if (ready || ConfirmedPeers.Contains(player.netId))
            {
                confirmed++;
            }
        }

        return total >= 2 && confirmed >= total;
    }

    /// <summary>
    /// The character module is the only place the local player can toggle ready, and its UI is torn down once
    /// the cup module takes over. Latch the last value instead of reading a dead UI, otherwise the local
    /// player un-confirms itself the moment it advances to map selection.
    /// </summary>
    internal static bool RefreshLocalConfirmed()
    {
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var slot = LocalSlot;
        if (boxes is not null && slot >= 0 && slot < boxes.Length && boxes[slot])
        {
            _localConfirmed = boxes[slot].ready;
        }

        return _localConfirmed;
    }

    private static void SyncLocalConfirmed()
    {
        var local = FRNetworkPlayer.localPlayer;
        if (local)
        {
            MarkPeerConfirmed(local.netId, RefreshLocalConfirmed());
        }
    }

    private static void EnsureClientReady()
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
            LoggerInstance.Warning($"[Online] NetworkClient.Ready: {exception.Message}");
        }
    }

    private static SelectionMenuBehaviour? FindSelectionMenu()
    {
        var menu = FindObjectOfType<SelectionMenuBehaviour>();
        return menu && menu._currentModule is not null ? menu : null;
    }

    private static void PrunePeers()
    {
        var live = new HashSet<uint>();
        foreach (var player in GetNetworkPlayers())
        {
            live.Add(player.netId);
        }

        ConfirmedPeers.RemoveWhere(id => !live.Contains(id));
    }

    internal static void LeaveSession(string reason) => EndSession(reason, stopNetwork: true);

    internal static void HandleDisconnect() => EndSession("Disconnected from host.", stopNetwork: false);

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
        if (!IsMainMenu(scene) && LevelManager.instance)
        {
            LevelManager.instance.LoadMainMenu();
        }

        LoggerInstance.Msg(reason);
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
        Patches.RacerInfoSync.Reset();
        RaceProgress.Stop();
    }

    internal static void ApplyRemotePick(FRNetworkPlayer? player)
    {
        if (!player || !IsActive)
        {
            return;
        }

        var info = player!.Network_info._racerInfo;
        Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
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
        if (!player || !IsActive || character < 0)
        {
            return;
        }

        skin = Math.Max(skin, 0);
        vehicle = Math.Max(vehicle, 0);

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
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var box = boxes is not null && slot < boxes.Length ? boxes[slot] : null;
        if (LastRemotePicks.TryGetValue(player.netId, out var previous) && previous == pick)
        {
            if (box && box!.ready != ready)
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
        var slotUI = GameObject.Find(SlotUIMap[character]);
        LoggerInstance.Msg($"Slot UI: {slotUI?.name}, character: {character}, slot: {slot}");
        if (slotUI is not null)
        {
            behaviour.SelectCharacter(slotUI.GetComponent<CharacterSlotUI>(), slot);
        }
        if (box)
        {
            box!._ready = ready;
        }
    }

    internal static List<FRNetworkPlayer> GetNetworkPlayers()
    {
        var result = new List<FRNetworkPlayer>();
        var server = FRNetworkServer.instance;
        if (NetworkServer.active && server)
        {
            var players = server.GetPlayers();
            if (players is not null)
            {
                foreach (var pair in players)
                {
                    var player = pair.Value;
                    if (player && player.netId != 0)
                    {
                        result.Add(player);
                    }
                }
            }
        }

        else
        {
            var found = FindObjectsOfType<FRNetworkPlayer>(true);
            if (found is not null)
            {
                foreach (var player in found)
                {
                    if (player && player.netId != 0)
                    {
                        result.Add(player);
                    }
                }
            }
        }

        result.Sort(static (left, right) => left.netId.CompareTo(right.netId));
        return result;
    }

    internal static int ConnectedCount => GetNetworkPlayers().Count;

    private static int SlotOf(FRNetworkPlayer player, List<FRNetworkPlayer>? players = null)
    {
        if (!player)
        {
            return -1;
        }

        players ??= GetNetworkPlayers();
        var index = 0;
        foreach (var candidate in players)
        {
            if (candidate == player)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static void ApplySlots(List<FRNetworkPlayer>? netPlayers = null)
    {
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        if (!behaviour)
        {
            return;
        }

        netPlayers ??= GetNetworkPlayers();
        var local = FRNetworkPlayer.localPlayer;
        var localSlot = local ? SlotOf(local, netPlayers) : -1;
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
                    Patches.RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
                        out var character, out var skin, out var vehicle, out _);
                    human.character = character < 0 ? 0 : character;
                    human.skin = Math.Max(skin, 0);
                    human.vehicle = Math.Max(vehicle, 0);
                }
            }
            else if (GameManager.players is not null && index < GameManager.players.Length &&
                     GameManager.players[index] is not null)
            {
                GameManager.players[index]!._joined = false;
            }
        }

        OnlineMenuMod.Instance.EnsureSelectionInput();
        if (!behaviour._inited)
        {
            try
            {
                behaviour.Loaded(MaxSlots);
            }
            catch (Exception exception)
            {
                LoggerInstance.Warning($"[Online] CharacterSelectionBehaviour.Loaded: {exception.Message}");
            }
        }

        RefreshJoinedBoxes(behaviour);
        _slotsReady = true;
        _lastCount = Math.Min(netPlayers.Count, MaxSlots);
        ApplySelectors(behaviour, _lastCount);
        LastRemotePicks.Clear();
        ApplyAllRemotePicks(netPlayers);
    }

    private static void SanitizeHumanPick(HumanGamePlayer? human)
    {
        if (human is null)
        {
            return;
        }

        Patches.RacerInfoSync.DecodePick(human.character, human.skin, human.vehicle,
            out var character, out var skin, out var vehicle, out _);
        human.character = character < 0 ? 0 : character;
        human.skin = Math.Max(skin, 0);
        human.vehicle = Math.Max(vehicle, 0);
    }

    private static void RefreshJoinedBoxes(CharacterSelectionBehaviour behaviour)
    {
        var humans = GameManager.players;
        var boxes = behaviour._boxes;
        if (boxes is null)
        {
            return;
        }

        for (var index = 0; index < boxes.Length; index++)
        {
            var box = boxes[index];
            if (!box)
            {
                continue;
            }

            var human = humans is not null && index < humans.Length ? humans[index] : null;
            if (human is not null && human._joined)
            {
                box.Join();
                behaviour.RefreshMatching(index, human, human.character, human.skin, human.vehicle);
            }
            else
            {
                box.Leave();
            }
        }
    }

    private static void ApplySelectors(CharacterSelectionBehaviour behaviour, int playersCnt)
    {
        if (behaviour._selectors is null)
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

            selector.gameObject.SetActive(playersCnt > index);
        }
    }

    private static void ApplyAllRemotePicks(IEnumerable<FRNetworkPlayer>? players = null)
    {
        foreach (var player in players ?? GetNetworkPlayers())
        {
            ApplyRemotePick(player);
        }
    }
}
