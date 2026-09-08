using HarmonyLib;
using Il2Cpp;
using TFROnlineMenu.Select;
using TFROnlineMenu.Transport;

namespace TFROnlineMenu.Race;


[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.SelectCharacter))]
internal static class SelectCharacterSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.SubmitCharacter))]
internal static class SubmitCharacterSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.RefreshMatching))]
internal static class RefreshMatchingSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Move")]
internal static class PlayerBoxMoveSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Submit")]
internal static class PlayerBoxSubmitSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(PlayerBoxUI), "ISelectable_Cancel")]
internal static class PlayerBoxCancelSyncPatch
{
    private static void Postfix(int humanindex)
    {
        if (humanindex == SelectionRoster.LocalSlot)
        {
            PlayerStatusSync.PushLocal();
        }
    }
}

[HarmonyPatch(typeof(FRNetworkPlayer), nameof(FRNetworkPlayer.ServerInit))]
internal static class ServerInitPickPatch
{
    private static void Postfix(FRNetworkPlayer __instance)
    {
        SelectionSlots.ApplyRemote(__instance);
    }
}
