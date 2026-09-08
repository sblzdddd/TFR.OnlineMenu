using Il2Cpp;
using TFROnlineMenu.Transport;
using static UnityEngine.Object;

namespace TFROnlineMenu.Select;

internal static class SelectionReady
{
    private static bool _localConfirmed;
    private static readonly HashSet<uint> Confirmed = new();

    internal static void ClearLocal() => _localConfirmed = false;

    internal static void Reset()
    {
        _localConfirmed = false;
        Confirmed.Clear();
    }

    internal static void Mark(uint netId, bool confirmed)
    {
        if (netId == 0)
        {
            return;
        }

        if (confirmed)
        {
            Confirmed.Add(netId);
        }
        else
        {
            Confirmed.Remove(netId);
        }
    }

    internal static bool AllConfirmed(out int confirmed, out int total)
    {
        Prune();
        var players = SelectionRoster.GetPlayers();
        total = players.Count;
        confirmed = 0;
        foreach (var player in players)
        {
            PlayerStatusSync.TryGetPick(player, out _, out _, out _, out var ready);
            if (ready || Confirmed.Contains(player.netId))
            {
                confirmed++;
            }
        }

        return total >= 2 && confirmed >= total;
    }

    /// <summary>
    /// Latch ready from the character module. That UI is torn down on map select, so reading it
    /// afterwards would un-confirm the local player.
    /// </summary>
    internal static bool RefreshLocal()
    {
        var behaviour = FindObjectOfType<CharacterSelectionBehaviour>();
        var boxes = behaviour ? behaviour._boxes : null;
        var slot = SelectionRoster.LocalSlot;
        if (boxes is not null && slot >= 0 && slot < boxes.Length && boxes[slot])
        {
            _localConfirmed = boxes[slot]!.ready;
        }

        return _localConfirmed;
    }

    internal static void SyncLocal()
    {
        var local = FRNetworkPlayer.localPlayer;
        if (local)
        {
            Mark(local.netId, RefreshLocal());
        }
    }

    internal static void Prune()
    {
        var live = new HashSet<uint>();
        foreach (var player in SelectionRoster.GetPlayers())
        {
            live.Add(player.netId);
        }

        Confirmed.RemoveWhere(id => !live.Contains(id));
        PlayerStatusSync.Prune(live);
    }
}
