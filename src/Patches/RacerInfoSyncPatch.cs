using HarmonyLib;
using Il2Cpp;

namespace TFROnlineMenu.Patches;

internal static class RacerInfoSync
{
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
        var client = FRNetworkClient.instance;
        if (!client)
        {
            return;
        }

        var info = client._clientInfo;
        var racer = info._racerInfo;
        racer._character = human.character;
        racer._skin = human.skin;
        racer._vehicle = human.vehicle;
        info._racerInfo = racer;
        client._clientInfo = info;

        var local = FRNetworkPlayer.localPlayer;
        if (local)
        {
            local.Network_info = info;
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.SelectCharacter))]
internal static class SelectCharacterSyncPatch
{
    static void Postfix(int humanindex)
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
    static void Postfix(int humanindex)
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
    static void Postfix(int humanindex)
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
    static void Postfix(int humanindex)
    {
        if (humanindex == OnlineSelection.LocalSlot)
        {
            RacerInfoSync.PushLocal();
        }
    }
}

