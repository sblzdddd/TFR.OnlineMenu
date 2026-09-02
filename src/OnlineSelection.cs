using Il2Cpp;
using Il2CppMirror;
using UnityEngine;

namespace TFROnlineMenu;

internal static class OnlineSelection
{
    internal const string SelectionScene = "selection";
    const int MaxSlots = 4;

    static bool _entering;
    static bool _slotsReady;
    static int _lastCount;
    static readonly Dictionary<uint, (int Character, int Skin, int Vehicle)> LastRemotePicks = new();

    internal static bool IsActive { get; private set; }
    internal static int LocalSlot => 0;

    internal static bool IsOnlineSession =>
        NetworkServer.active || NetworkClient.active;

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
        NetworkManager.networkSceneName = SelectionScene;
        NetworkServer.SetAllClientsNotReady();
        NetworkServer.SendToAll(new SceneMessage
        {
            sceneName = SelectionScene,
            sceneOperation = SceneOperation.Normal,
            customHandling = true
        });
        EnterSelectionLocally();
        OnlineMenuMod.Instance.Message = "Starting online Quick Race selection...";
    }

    internal static void EnterSelectionLocally()
    {
        if (_entering)
        {
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
        _slotsReady = false;
        _lastCount = 0;
        LastRemotePicks.Clear();

        var characters = new CharacterSelectionModule("characters", MaxSlots);
        var cups = new CupSelectionModule("cups", true);
        characters.next = cups;
        cups.prev = characters;
        menu.GoToSelection(characters, null);
    }

    internal static void HandleSceneInitialized(string sceneName)
    {
        if (!sceneName.Equals(SelectionScene, StringComparison.OrdinalIgnoreCase))
        {
            if (IsActive && !sceneName.Equals("loading", StringComparison.OrdinalIgnoreCase))
            {
                _entering = false;
                _slotsReady = false;
            }

            return;
        }

        if (!IsOnlineSession)
        {
            return;
        }

        IsActive = true;
        _entering = false;
        ApplySlots();
    }

    internal static void Tick()
    {
        TryFollowHost();
        if (!IsActive || !IsOnlineSession)
        {
            return;
        }

        if (!UnityEngine.Object.FindObjectOfType<SelectionMenuBehaviour>())
        {
            return;
        }

        var count = GetNetworkPlayers().Count;
        if (!_slotsReady || count != _lastCount)
        {
            ApplySlots();
        }

        ApplyAllRemotePicks();
    }

    static void TryFollowHost()
    {
        if (IsActive || _entering || NetworkServer.active || !NetworkClient.active)
        {
            return;
        }

        if (!string.Equals(NetworkManager.networkSceneName, SelectionScene, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnterSelectionLocally();
    }

    internal static void Stop()
    {
        IsActive = false;
        _entering = false;
        _slotsReady = false;
        _lastCount = 0;
        LastRemotePicks.Clear();
    }

    internal static void ApplyRemotePick(FRNetworkPlayer? player)
    {
        if (!player || !IsActive)
        {
            return;
        }

        var slot = SlotOf(player!);
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

        var info = player!.Network_info._racerInfo;
        var pick = (info._character, info._skin, info._vehicle);
        if (LastRemotePicks.TryGetValue(player.netId, out var previous) && previous == pick)
        {
            return;
        }

        LastRemotePicks[player.netId] = pick;
        var behaviour = UnityEngine.Object.FindObjectOfType<CharacterSelectionBehaviour>();
        if (!behaviour)
        {
            human.character = info._character;
            human.skin = info._skin;
            human.vehicle = info._vehicle;
            return;
        }

        behaviour.RefreshMatching(slot, human, info._character, info._skin, info._vehicle);
    }

    internal static List<FRNetworkPlayer> GetNetworkPlayers()
    {
        var result = new List<FRNetworkPlayer>();
        var found = UnityEngine.Object.FindObjectsOfType<FRNetworkPlayer>();
        if (found is null)
        {
            return result;
        }

        foreach (var player in found)
        {
            if (player)
            {
                result.Add(player);
            }
        }

        result.Sort(static (left, right) => left.netId.CompareTo(right.netId));
        return result;
    }

    internal static int ConnectedCount => GetNetworkPlayers().Count;

    static int SlotOf(FRNetworkPlayer player)
    {
        var local = FRNetworkPlayer.localPlayer;
        if (local && player == local)
        {
            return LocalSlot;
        }

        var remotes = 0;
        foreach (var candidate in GetNetworkPlayers())
        {
            if (local && candidate == local)
            {
                continue;
            }

            if (candidate == player)
            {
                return remotes + 1;
            }

            remotes++;
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

        var local = FRNetworkPlayer.localPlayer;
        var remotes = new List<FRNetworkPlayer>();
        foreach (var candidate in GetNetworkPlayers())
        {
            if (!local || candidate != local)
            {
                remotes.Add(candidate);
            }
        }

        var localHuman = GameManager.players is not null && GameManager.players.Length > 0
            ? GameManager.players[0]
            : null;
        localHuman ??= GameManager.AddHuman(0);
        if (localHuman is not null)
        {
            localHuman._joined = true;
        }

        for (var index = 1; index < MaxSlots; index++)
        {
            var remoteIndex = index - 1;
            if (remoteIndex < remotes.Count)
            {
                var human = GameManager.AddHuman(index);
                if (human is null)
                {
                    continue;
                }

                human._joined = true;
                var info = remotes[remoteIndex].Network_info._racerInfo;
                if (info._character >= 0)
                {
                    human.character = info._character;
                    human.skin = info._skin;
                    human.vehicle = info._vehicle;
                }
            }
            else if (GameManager.players is not null && index < GameManager.players.Length &&
                     GameManager.players[index] is not null)
            {
                GameManager.players[index]._joined = false;
            }
        }

        behaviour.Loaded(MaxSlots);
        HideRemoteSelectors();
        _slotsReady = true;
        _lastCount = Math.Clamp(remotes.Count + 1, 1, MaxSlots);
        ApplyAllRemotePicks();
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
