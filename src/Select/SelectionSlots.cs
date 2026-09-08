using Il2Cpp;
using TFROnlineMenu.Transport;
using UnityEngine;
using static UnityEngine.Object;

namespace TFROnlineMenu.Select;

internal static class SelectionSlots
{
    internal const int Max = 4;

    private static bool _ready;
    private static int _lastCount;
    private static readonly Dictionary<uint, (int Character, int Skin, int Vehicle)> LastRemotePicks = new();

    private static readonly string[] SlotUiMap =
    {
        "CharacterSlot (6)", "CharacterSlot (15)", "CharacterSlot (32)", "CharacterSlot (10)", "CharacterSlot (2)",
        "CharacterSlot (31)", "CharacterSlot (1)", "CharacterSlot (27)", "CharacterSlot (30)", "CharacterSlot (17)",
        "CharacterSlot (16)", "CharacterSlot", "CharacterSlot (12)", "CharacterSlot (26)", "CharacterSlot (28)",
        "CharacterSlot (3)", "CharacterSlot (5)", "CharacterSlot (18)", "CharacterSlot (7)", "CharacterSlot (11)",
        "CharacterSlot (29)", "CharacterSlot (8)", "CharacterSlot (13)", "CharacterSlot (9)",
    };

    internal static void Reset()
    {
        _ready = false;
        _lastCount = 0;
        LastRemotePicks.Clear();
    }

    internal static void InstallSequence(MainMenuManager? menu = null)
    {
        menu ??= MainMenuManager.instance;
        if (!menu || menu._startModule is not null)
        {
            return;
        }

        var characters = new CharacterSelectionModule("characters");
        var cups = new CupSelectionModule("cups", true);
        characters.next = cups;
        cups.prev = characters;
        menu._startModule = characters;
    }

    internal static SelectionMenuBehaviour? FindMenu()
    {
        var menu = FindObjectOfType<SelectionMenuBehaviour>();
        return menu && menu._currentModule is not null ? menu : null;
    }

    internal static void ApplyRemote(FRNetworkPlayer? player)
    {
        if (player is null || !player || !OnlineSelection.IsActive ||
            !PlayerStatusSync.TryGetPick(player, out var character, out var skin, out var vehicle, out var ready))
        {
            return;
        }

        if (player != FRNetworkPlayer.localPlayer && (ready || character >= 0))
        {
            SelectionReady.Mark(player.netId, ready);
        }

        ApplyRemote(player, character, skin, vehicle, ready);
    }

    internal static void ApplyRemote(FRNetworkPlayer player, int character, int skin, int vehicle, bool ready = false)
    {
        if (!player || !OnlineSelection.IsActive || character < 0)
        {
            return;
        }

        var slot = SelectionRoster.SlotOf(player);
        if (slot < 0 || slot == SelectionRoster.LocalSlot)
        {
            return;
        }

        var humans = GameManager.players;
        var human = humans is not null && slot < humans.Length ? humans[slot] : null;
        if (human is null)
        {
            return;
        }

        skin = Math.Max(skin, 0);
        vehicle = Math.Max(vehicle, 0);
        if (ready)
        {
            SelectionReady.Mark(player.netId, true);
        }

        var pick = (character, skin, vehicle);
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var box = boxes is not null && slot < boxes.Length ? boxes[slot] : null;
        if (LastRemotePicks.TryGetValue(player.netId, out var previous) && previous == pick)
        {
            if (box is not null && box && box.ready != ready)
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
        if (character < SlotUiMap.Length)
        {
            var slotUi = GameObject.Find(SlotUiMap[character]);
            if (slotUi is not null)
            {
                behaviour.SelectCharacter(slotUi.GetComponent<CharacterSlotUI>(), slot);
            }
        }

        if (box is not null && box)
        {
            box._ready = ready;
        }
    }

    internal static void Tick()
    {
        if (FindMenu() is null)
        {
            return;
        }

        var players = SelectionRoster.GetPlayers();
        var count = Math.Min(players.Count, Max);
        if (_ready && count == _lastCount)
        {
            ApplyAllRemote(players);
            return;
        }

        try
        {
            Apply(players);
        }
        catch (Exception exception)
        {
            OnlineMenuMod.Instance.LoggerInstance.Warning($"[Online] ApplySlots: {exception.Message}");
            _ready = true;
            _lastCount = count;
        }
    }

    internal static void Apply(List<FRNetworkPlayer>? netPlayers = null)
    {
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        if (!behaviour)
        {
            return;
        }

        netPlayers ??= SelectionRoster.GetPlayers();
        var local = FRNetworkPlayer.localPlayer;
        for (var index = 0; index < Max; index++)
        {
            if (index < netPlayers.Count)
            {
                Bind(index, netPlayers[index], local);
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
                behaviour.Loaded();
            }
            catch (Exception exception)
            {
                OnlineMenuMod.Instance.LoggerInstance.Warning(
                    $"[Online] CharacterSelectionBehaviour.Loaded: {exception.Message}");
            }
        }

        RefreshJoinedBoxes(behaviour);
        _ready = true;
        _lastCount = Math.Min(netPlayers.Count, Max);
        ApplySelectors(behaviour, _lastCount);
        LastRemotePicks.Clear();
        ApplyAllRemote(netPlayers);
    }

    private static void Bind(int index, FRNetworkPlayer netPlayer, FRNetworkPlayer? local)
    {
        var human = GameManager.AddHuman(index);
        if (human is null)
        {
            return;
        }

        human._joined = true;
        human.character = Math.Max(human.character, 0);
        human.skin = Math.Max(human.skin, 0);
        human.vehicle = Math.Max(human.vehicle, 0);
        if (local && netPlayer == local)
        {
            return;
        }

        if (PlayerStatusSync.TryGetPick(netPlayer, out var character, out var skin, out var vehicle, out _))
        {
            human.character = Math.Max(character, 0);
            human.skin = Math.Max(skin, 0);
            human.vehicle = Math.Max(vehicle, 0);
        }
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
                if (box != null) box.Join();
                behaviour.RefreshMatching(index, human, human.character, human.skin, human.vehicle);
            }
            else
            {
                if (box != null) box.Leave();
            }
        }
    }

    private static void ApplySelectors(CharacterSelectionBehaviour behaviour, int playerCount)
    {
        if (behaviour._selectors is null)
        {
            return;
        }

        for (var index = 0; index < behaviour._selectors.Length; index++)
        {
            var selector = behaviour._selectors[index];
            if (selector != null) selector.gameObject.SetActive(playerCount > index);
        }
    }

    private static void ApplyAllRemote(IEnumerable<FRNetworkPlayer> players)
    {
        foreach (var player in players)
        {
            ApplyRemote(player);
        }
    }
}
