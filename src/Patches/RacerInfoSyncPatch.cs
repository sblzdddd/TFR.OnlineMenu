using HarmonyLib;
using Il2Cpp;
using Il2CppMirror;
using MelonLoader;
using TFROnlineMenu.Select;
using UnityEngine;

namespace TFROnlineMenu.Patches;

internal static class RacerInfoSync
{
    private const double PickRttBase = 1_000_000d;
    private const double PickReadyFlag = 500_000_000d;
    private const double PickFinishFlag = 2_000_000_000d;
    private const int PickStride = 1000;
    internal const int ReadyBias = 100;
    internal const int LobbyBias = 1000;
    internal const int FinishBias = 10000;

    private static (int Character, int Skin, int Vehicle, bool Ready)? _lastPushed;
    private static float _nextHeartbeat;

    internal static void Reset()
    {
        _lastPushed = null;
        _nextHeartbeat = 0;
    }

    internal static void PushLocal()
    {
        if (!OnlineSelection.IsActive || !OnlineSelection.IsOnlineSession)
        {
            return;
        }

        var humans = GameManager.players;
        var slot = OnlineSelection.LocalSlot;
        if (humans is null || slot < 0 || slot >= humans.Length || humans[slot] is null)
        {
            return;
        }

        var human = humans[slot];
        var ready = OnlineSelection.RefreshLocalConfirmed();
        var pick = (human.character, human.skin, human.vehicle, ready);
        if (_lastPushed == pick && Time.unscaledTime < _nextHeartbeat)
        {
            return;
        }

        var client = FRNetworkClient.instance;
        var local = FRNetworkPlayer.localPlayer;
        if (!client || !local)
        {
            return;
        }

        var info = client._clientInfo;
        info._racerInfo = new FRNetworkClient.SNetRacerInfo
        {
            _character = EncodeCharacter(human.character, ready, inLobby: true),
            _skin = human.skin,
            _vehicle = human.vehicle
        };
        client._clientInfo = info;
        var packed = PackPick(human.character, human.skin, human.vehicle, ready);

        if (NetworkServer.active)
        {
            local.Network_info = info;
            local.Network_rtt = packed;
            OnlineSelection.MarkPeerConfirmed(local.netId, ready);
            _lastPushed = pick;
            _nextHeartbeat = Time.unscaledTime + 0.35f;
            return;
        }

        // Never re-send SFumoClient: the server handler for it is FRNetworkServer.OnServerConnected, the
        // join handshake. It spawns another player and does _players.Add(conn, ...), so a second one throws
        // a duplicate-key exception and Mirror drops the connection. CmdPing is the update channel.
        local.CmdPing(packed);
        OnlineSelection.MarkPeerConfirmed(local.netId, ready);
        _lastPushed = pick;
        _nextHeartbeat = Time.unscaledTime + 0.35f;
    }

    internal static void DecodePick(int rawCharacter, int skin, int vehicle,
        out int character, out int decodedSkin, out int decodedVehicle, out bool ready)
    {
        var value = rawCharacter >= FinishBias ? rawCharacter - FinishBias : rawCharacter;
        if (value >= LobbyBias)
        {
            value -= LobbyBias;
        }

        ready = value >= ReadyBias;
        character = ready ? value - ReadyBias : value;
        decodedSkin = skin;
        decodedVehicle = vehicle;
    }

    internal static bool DecodeLobby(int rawCharacter) =>
        (rawCharacter >= FinishBias ? rawCharacter - FinishBias : rawCharacter) >= LobbyBias;

    internal static bool DecodeFinished(int rawCharacter) => rawCharacter >= FinishBias;

    internal static int EncodeCharacter(int character, bool ready, bool inLobby = false, bool finished = false)
    {
        var safe = Math.Max(character, 0);
        if (ready)
        {
            safe += ReadyBias;
        }

        if (inLobby)
        {
            safe += LobbyBias;
        }

        return finished ? safe + FinishBias : safe;
    }

    /// <summary>
    /// Re-publishes the local pick with the "crossed the finish line" bit toggled. The lobby pick itself is
    /// left alone so the racer keeps its character and kart on the peers that are still driving.
    /// </summary>
    internal static void PushLocalFinish(bool finished)
    {
        var local = FRNetworkPlayer.localPlayer;
        if (!local)
        {
            return;
        }

        var current = local.Network_info._racerInfo;
        DecodePick(current._character, current._skin, current._vehicle,
            out var character, out var skin, out var vehicle, out var ready);

        if (NetworkServer.active)
        {
            var info = local.Network_info;
            info._racerInfo = new FRNetworkClient.SNetRacerInfo
            {
                _character = EncodeCharacter(character, ready, DecodeLobby(current._character), finished),
                _skin = skin,
                _vehicle = vehicle
            };
            local.Network_info = info;
            return;
        }

        local.CmdPing(PackPick(character, skin, vehicle, ready, finished));
    }

    internal static bool TryApplyPackedRtt(FRNetworkPlayer player, double value, out bool skipOriginalSetter)
    {
        skipOriginalSetter = false;
        if (!TryUnpackPick(value, out var character, out var skin, out var vehicle, out var ready, out var finished))
        {
            return false;
        }

        skipOriginalSetter = !NetworkServer.active;
        if (!player)
        {
            return true;
        }

        if (NetworkServer.active)
        {
            var info = player.Network_info;
            info._racerInfo = new FRNetworkClient.SNetRacerInfo
            {
                // The packed rtt carries no lobby bit, so keep the one the peer already advertised.
                _character = EncodeCharacter(character, ready, DecodeLobby(info._racerInfo._character), finished),
                _skin = skin,
                _vehicle = vehicle
            };
            player.Network_info = info;
            OnlineSelection.MarkPeerConfirmed(player.netId, ready);
        }

        OnlineSelection.ApplyRemotePick(player, character, skin, vehicle, ready);
        return true;
    }

    private static double PackPick(int character, int skin, int vehicle, bool ready, bool finished = false)
    {
        return PickRttBase
               + (ready ? PickReadyFlag : 0d)
               + (finished ? PickFinishFlag : 0d)
               + SafePick(character) * PickStride * PickStride
               + SafePick(skin) * PickStride
               + SafePick(vehicle);
    }

    private static bool TryUnpackPick(double value, out int character, out int skin, out int vehicle, out bool ready,
        out bool finished)
    {
        character = 0;
        skin = 0;
        vehicle = 0;
        ready = false;
        finished = false;
        if (double.IsNaN(value) || value < PickRttBase)
        {
            return false;
        }

        var magnitude = value;
        if (magnitude >= PickRttBase + PickFinishFlag)
        {
            finished = true;
            magnitude -= PickFinishFlag;
        }

        if (magnitude >= PickRttBase + PickReadyFlag)
        {
            ready = true;
            magnitude -= PickReadyFlag;
        }

        // Anything still above the ready flag is not one of our packed picks; let the real rtt through.
        if (magnitude >= PickRttBase + PickReadyFlag)
        {
            return false;
        }

        var packed = (int)Math.Round(magnitude - PickRttBase);
        character = packed / (PickStride * PickStride);
        skin = packed / PickStride % PickStride;
        vehicle = packed % PickStride;
        return true;
    }

    private static int SafePick(int value) => Math.Clamp(value, 0, PickStride - 1);
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.SelectCharacter))]
internal static class SelectCharacterSyncPatch
{
    private static MelonLogger.Instance LoggerInstance => TFROnlineMenu.OnlineMenuMod.Instance.LoggerInstance;
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.SubmitCharacter))]
internal static class SubmitCharacterSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.RefreshMatching))]
internal static class RefreshMatchingSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Move")]
internal static class PlayerBoxMoveSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Submit")]
internal static class PlayerBoxSubmitSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Cancel")]
internal static class PlayerBoxCancelSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(FRNetworkPlayer), nameof(FRNetworkPlayer.ServerInit))]
internal static class ServerInitPickPatch
{
    private static void Postfix(FRNetworkPlayer __instance)
    {
        OnlineSelection.ApplyRemotePick(__instance);
    }
}

[HarmonyPatch(typeof(FRNetworkPlayer), "UserCode_CmdPing")]
internal static class CmdPingPickPatch
{
    private static void Prefix(FRNetworkPlayer __instance, double rtt)
    {
        RacerInfoSync.TryApplyPackedRtt(__instance, rtt, out _);
    }
}

[HarmonyPatch(typeof(FRNetworkPlayer), nameof(FRNetworkPlayer.Network_rtt), MethodType.Setter)]
internal static class NetworkRttPickPatch
{
    private static bool Prefix(FRNetworkPlayer __instance, double value)
    {
        return !RacerInfoSync.TryApplyPackedRtt(__instance, value, out var skipOriginalSetter) || !skipOriginalSetter;
    }
}

[HarmonyPatch(typeof(FRNetworkPlayer), nameof(FRNetworkPlayer.Network_info), MethodType.Setter)]
internal static class NetworkInfoPickPatch
{
    private static void Postfix(FRNetworkPlayer __instance)
    {
        OnlineSelection.ApplyRemotePick(__instance);
    }
}
