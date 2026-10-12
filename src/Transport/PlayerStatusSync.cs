using Il2Cpp;
using Il2CppMirror;
using MelonLoader;
using TFROnlineMenu.Select;
using TFROnlineMenu.Transport.Messages;

namespace TFROnlineMenu.Transport;

internal static class PlayerStatusSync
{
    private static readonly Dictionary<uint, FumoStatesMsg> States = new();
    private static FumoStatesMsg _lastLocal;
    private static (int Character, int Skin, int Vehicle, bool Ready, bool InLobby)? _lastPushed;
    private static bool _advertiseLobby;
    private static MelonLogger.Instance Logger => OnlineMenuMod.Instance.LoggerInstance;

    internal static void Reset()
    {
        States.Clear();
        _lastLocal = default;
        _lastPushed = null;
        _advertiseLobby = false;
    }

    internal static void SetLobbyAdvertised(bool inLobby)
    {
        if (_advertiseLobby == inLobby)
        {
            return;
        }

        _advertiseLobby = inLobby;
        Logger.Msg($"lobby advertised={inLobby}");
        PushLocal();
    }

    internal static void PushLocal()
    {
        if (!OnlineSelection.IsActive || !OnlineSelection.IsOnlineSession)
        {
            return;
        }

        if (!TryBuildLocal(finished: false, out var msg))
        {
            return;
        }

        var pick = (msg.Character, msg.Skin, msg.Vehicle, msg.Ready, msg.InLobby);
        if (_lastPushed == pick)
        {
            return;
        }

        Publish(msg);
        _lastPushed = pick;
    }

    internal static void PushLocalFinish(bool finished)
    {
        if (!TryBuildLocal(finished, out var msg))
        {
            return;
        }

        msg.InLobby = false;
        Publish(msg);
    }

    internal static bool TryGetPick(FRNetworkPlayer? player,
        out int character, out int skin, out int vehicle, out bool ready)
    {
        character = 0;
        skin = 0;
        vehicle = 0;
        ready = false;
        if (player is null || !player || !States.TryGetValue(player.netId, out var status))
        {
            return false;
        }

        character = status.Character;
        skin = status.Skin;
        vehicle = status.Vehicle;
        ready = status.Ready;
        return true;
    }

    internal static bool IsFinished(FRNetworkPlayer? player) =>
        player is not null && player && States.TryGetValue(player.netId, out var status) && status.Finished;

    internal static bool AnyInLobby()
    {
        foreach (var status in States.Values)
        {
            if (status.InLobby)
            {
                return true;
            }
        }

        return false;
    }

    internal static void Prune(HashSet<uint> liveIds)
    {
        var stale = new List<uint>();
        foreach (var id in States.Keys)
        {
            if (!liveIds.Contains(id))
            {
                stale.Add(id);
            }
        }

        foreach (var id in stale)
        {
            States.Remove(id);
        }
    }

    internal static void Handle(NetworkConnection? connection, FumoStatesMsg msg, bool server)
    {
        if (server)
        {
            var identity = connection?.identity;
            if (identity is not null && identity)
            {
                msg.NetId = identity.netId;
            }

            Store(msg);
            Apply(msg);
            Logger.Msg(
                $"server status net={msg.NetId} char={msg.Character} skin={msg.Skin} ready={msg.Ready} lobby={msg.InLobby} fin={msg.Finished}");
            var snapshot = msg;
            FumoTransport.Relay(FumoPacket.Kind.PlayerStatus, writer => snapshot.Serialize(writer), connection);
            return;
        }

        if (NetworkServer.active)
        {
            return;
        }

        Store(msg);
        Apply(msg);
        Logger.Msg(
            $"client status net={msg.NetId} char={msg.Character} ready={msg.Ready} lobby={msg.InLobby} fin={msg.Finished}");
    }

    internal static void SendSnapshot(NetworkConnection conn)
    {
        if (!NetworkServer.active) return;

        foreach (var status in States.Values)
        {
            var snapshot = status;
            FumoTransport.SendToConnection(conn, FumoPacket.Kind.PlayerStatus, snapshot.Serialize);
        }
    }

    private static bool TryBuildLocal(bool finished, out FumoStatesMsg msg)
    {
        msg = default;
        var local = FRNetworkPlayer.localPlayer;
        if (!local || local.netId == 0)
        {
            return false;
        }

        var humans = GameManager.players;
        var slot = SelectionRoster.LocalSlot;
        var character = _lastLocal.Character;
        var skin = _lastLocal.Skin;
        var vehicle = _lastLocal.Vehicle;
        if (humans is not null && slot >= 0 && slot < humans.Length && humans[slot] is not null)
        {
            var human = humans[slot];
            character = human!.character;
            skin = human.skin;
            vehicle = human.vehicle;
        }

        msg = new FumoStatesMsg
        {
            NetId = local.netId,
            Character = Math.Max(character, 0),
            Skin = Math.Max(skin, 0),
            Vehicle = Math.Max(vehicle, 0),
            Ready = SelectionReady.RefreshLocal(),
            InLobby = _advertiseLobby,
            Finished = finished
        };
        return true;
    }

    private static void Publish(FumoStatesMsg msg)
    {
        Store(msg);
        _lastLocal = msg;
        Logger.Msg(
            $"publish net={msg.NetId} char={msg.Character} skin={msg.Skin} ready={msg.Ready} lobby={msg.InLobby} fin={msg.Finished}");
        var snapshot = msg;
        FumoTransport.Send(FumoPacket.Kind.PlayerStatus, writer => snapshot.Serialize(writer));
    }

    private static void Store(FumoStatesMsg msg)
    {
        if (msg.NetId == 0)
        {
            return;
        }

        States[msg.NetId] = msg;
        SelectionReady.Mark(msg.NetId, msg.Ready);
    }

    private static void Apply(FumoStatesMsg msg)
    {
        var player = SelectionRoster.Find(msg.NetId);
        if (player is null || !player) return;

        SelectionSlots.ApplyRemote(player, msg.Character, msg.Skin, msg.Vehicle, msg.Ready);
    }
}
