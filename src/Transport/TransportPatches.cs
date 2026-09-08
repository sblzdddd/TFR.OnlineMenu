using HarmonyLib;
using Il2CppMirror;

namespace TFROnlineMenu.Transport;

[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.UnpackAndInvoke))]
internal static class ClientUnpackPatch
{
    private static bool Prefix(NetworkReader reader, int channelId, ref bool __result)
    {
        _ = channelId;
        try
        {
            if (!FumoTransport.TryHandle(NetworkClient.connection, reader, server: false))
            {
                return true;
            }

            __result = true;
            return false;
        }
        catch (Exception exception)
        {
            OnlineMenuMod.Instance.LoggerInstance.Warning($"client unpack: {exception.Message}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.UnpackAndInvoke))]
internal static class ServerUnpackPatch
{
    private static bool Prefix(NetworkConnectionToClient connection, NetworkReader reader, int channelId,
        ref bool __result)
    {
        _ = channelId;
        try
        {
            if (!FumoTransport.TryHandle(connection, reader, server: true))
            {
                return true;
            }

            __result = true;
            return false;
        }
        catch (Exception exception)
        {
            OnlineMenuMod.Instance.LoggerInstance.Warning($"server unpack: {exception.Message}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.RegisterSystemHandlers))]
internal static class ClientRegisterHandlersPatch
{
    private static void Postfix() => FumoTransport.EnsureHandlers();
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.RegisterMessageHandlers))]
internal static class ServerRegisterHandlersPatch
{
    private static void Postfix() => FumoTransport.EnsureHandlers();
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.SetClientReady))]
internal static class ClientReadySnapshotPatch
{
    private static void Postfix(NetworkConnection conn)
    {
        OnlineMenuMod.Instance.LoggerInstance.Msg(
            $"SetClientReady conn={conn.connectionId} ready={conn.isReady}");
        PlayerStatusSync.SendSnapshot(conn);
    }
}
