using Il2CppInterop.Runtime;
using Il2CppMirror;
using MelonLoader;
using TFROnlineMenu.Transport.Messages;

namespace TFROnlineMenu.Transport;

internal static class FumoTransport
{
    private static int _peekLogs;
    private static NetworkMessageDelegate? _clientDelegate;
    private static NetworkMessageDelegate? _serverDelegate;

    private static readonly Action<NetworkConnection, NetworkReader, int> ClientRegistered =
        static (conn, reader, channel) => HandleRegistered(conn, reader, channel, server: false);

    private static readonly Action<NetworkConnection, NetworkReader, int> ServerRegistered =
        static (conn, reader, channel) => HandleRegistered(conn, reader, channel, server: true);

    private static MelonLogger.Instance Logger => OnlineMenuMod.Instance.LoggerInstance;

    internal static void EnsureHandlers()
    {
        TryRegister(NetworkClient.handlers, client: true);
        TryRegister(NetworkServer.handlers, client: false);
    }

    internal static void Send(FumoPacket.Kind kind, Action<NetworkWriter> writeBody)
    {
        var writer = NetworkWriterPool.GetWriter();
        try
        {
            Pack(writer, kind, writeBody);
            var payload = writer.ToArraySegment();
            Logger.Msg(
                $"send {kind} bytes={writer.Position} segment={payload.Count} " +
                $"server={NetworkServer.active} client={NetworkClient.active} conns={ConnectionCount()}");
            Dispatch(payload);
        }
        finally
        {
            NetworkWriterPool.Recycle(writer);
        }
    }

    internal static void Relay(FumoPacket.Kind kind, Action<NetworkWriter> writeBody, NetworkConnection? excluded)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        var writer = NetworkWriterPool.GetWriter();
        try
        {
            Pack(writer, kind, writeBody);
            SendToClients(writer.ToArraySegment(), excluded);
        }
        finally
        {
            NetworkWriterPool.Recycle(writer);
        }
    }

    internal static void SendToConnection(NetworkConnection conn, FumoPacket.Kind kind, Action<NetworkWriter> writeBody)
    {

        var writer = NetworkWriterPool.GetWriter();
        try
        {
            Pack(writer, kind, writeBody);
            conn.Send(writer.ToArraySegment());
            Logger.Msg($"snapshot {kind} -> conn={conn.connectionId}");
        }
        finally
        {
            NetworkWriterPool.Recycle(writer);
        }
    }

    internal static bool TryHandle(NetworkConnection? connection, NetworkReader reader, bool server)
    {
        var origin = reader.Position;
        if (reader.Remaining < sizeof(ushort) + sizeof(byte))
        {
            return false;
        }

        var id = reader.ReadUShort();
        if (_peekLogs < 12)
        {
            _peekLogs++;
            Logger.Msg(
                $"unpack peek id=0x{id:X4} rem={reader.Remaining} server={server} " +
                $"conn={connection?.connectionId}");
        }

        if (id != FumoPacket.Id)
        {
            reader.Position = origin;
            return false;
        }

        HandleBody(connection, reader, server);
        return true;
    }

    private static void HandleRegistered(NetworkConnection conn, NetworkReader reader, int channelId, bool server)
    {
        _ = channelId;
        HandleBody(conn, reader, server);
    }

    private static void Pack(NetworkWriter writer, FumoPacket.Kind kind, Action<NetworkWriter> writeBody)
    {
        writer.WriteUShort(FumoPacket.Id);
        writer.WriteByte((byte)kind);
        writeBody(writer);
    }

    private static void Dispatch(Il2CppSystem.ArraySegment<byte> payload)
    {
        if (NetworkServer.active)
        {
            SendToClients(payload, NetworkServer.localConnection);
            return;
        }

        var connection = NetworkClient.connection;
        if (NetworkClient.active && connection != null)
        {
            connection.Send(payload);
        }
    }

    private static void SendToClients(Il2CppSystem.ArraySegment<byte> payload, NetworkConnection? excluded)
    {
        var connections = NetworkServer.connections;
        if (connections == null)
        {
            Logger.Warning("relay skipped: NetworkServer.connections is null");
            return;
        }

        var excludedId = excluded?.connectionId;
        var ids = new List<int>(connections.Count);
        foreach (var pair in connections)
        {
            ids.Add(pair.Key);
        }

        var sent = 0;
        foreach (var id in ids)
        {
            if (!connections.TryGetValue(id, out var conn) || conn == null)
            {
                continue;
            }

            if (excludedId is { } skip && conn.connectionId == skip)
            {
                continue;
            }

            conn.Send(payload);
            sent++;
        }

        Logger.Msg($"relayed to {sent}/{connections.Count} connection(s) excluded={excludedId}");
    }

    private static int ConnectionCount()
    {
        var connections = NetworkServer.connections;
        return connections == null ? 0 : connections.Count;
    }

    private static void HandleBody(NetworkConnection? connection, NetworkReader reader, bool server)
    {
        var kind = (FumoPacket.Kind)reader.ReadByte();
        Logger.Msg($"recv {kind} server={server} conn={connection?.connectionId}");
        try
        {
            switch (kind)
            {
                case FumoPacket.Kind.PlayerStatus:
                    PlayerStatusSync.Handle(connection, FumoStatesMsg.Deserialize(reader), server);
                    break;
                default:
                    Logger.Warning($"Unknown packet kind {(byte)kind}.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Logger.Warning($"Failed to handle {kind}: {exception.Message}");
        }
    }

    private static void TryRegister(
        Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate>? handlers,
        bool client)
    {
        if (handlers == null || handlers.ContainsKey(FumoPacket.Id))
        {
            return;
        }

        try
        {
            var converted = client ? _clientDelegate : _serverDelegate;
            if (converted == null)
            {
                converted = DelegateSupport.ConvertDelegate<NetworkMessageDelegate>(
                    client ? ClientRegistered : ServerRegistered);
                if (converted == null)
                {
                    Logger.Warning($"ConvertDelegate returned null ({(client ? "client" : "server")})");
                    return;
                }

                if (client)
                {
                    _clientDelegate = converted;
                }
                else
                {
                    _serverDelegate = converted;
                }
            }

            handlers[FumoPacket.Id] = converted;
            Logger.Msg($"registered {(client ? "client" : "server")} handler id=0x{FumoPacket.Id:X4}");
        }
        catch (Exception exception)
        {
            Logger.Warning($"handler register failed: {exception.Message}");
        }
    }
}
