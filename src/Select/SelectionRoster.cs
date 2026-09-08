using Il2Cpp;
using Il2CppMirror;
using static UnityEngine.Object;

namespace TFROnlineMenu.Select;

internal static class SelectionRoster
{
    internal static int Count => GetPlayers().Count;

    internal static int LocalSlot => FRNetworkPlayer.localPlayer ? SlotOf(FRNetworkPlayer.localPlayer) : -1;

    internal static List<FRNetworkPlayer> GetPlayers()
    {
        var result = new List<FRNetworkPlayer>();
        var found = new List<FRNetworkPlayer>();
        var server = FRNetworkServer.instance;
        if (NetworkServer.active && server)
            foreach (var entry in server.GetPlayers())
                found.Add(entry.Value);
        else
            found = [.. FindObjectsOfType<FRNetworkPlayer>(true)];

        result.AddRange(found.Where(player => player.netId != 0));
        result.Sort(static (left, right) => left.netId.CompareTo(right.netId));
        return result;
    }

    internal static FRNetworkPlayer? Find(uint netId)
    {
        return netId == 0 ? null : GetPlayers().FirstOrDefault(player => player.netId == netId);
    }

    internal static int SlotOf(FRNetworkPlayer player, List<FRNetworkPlayer>? players = null)
    {
        if (!player) return -1;
        players ??= GetPlayers();
        return players.IndexOf(player);
    }
}
