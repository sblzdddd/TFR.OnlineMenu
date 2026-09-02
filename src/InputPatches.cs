using HarmonyLib;
using Il2Cpp;
using UnityEngine.InputSystem;

namespace TFROnlineMenu;

[HarmonyPatch(typeof(RacingInputManager), "OnPlayerJoined")]
internal static class RacingInputManagerOnPlayerJoinedPatch
{
    private static void Postfix(RacingInputManager __instance, PlayerInput __0)
    {
        OnlineMenuMod.Instance?.OnRacingPlayerJoined(__instance, __0);
    }
}

[HarmonyPatch(typeof(HumanGamePlayer), "OnPossessed")]
internal static class HumanGamePlayerOnPossessedPatch
{
    private static void Postfix(HumanGamePlayer __instance, PlayerRacer __0, int __2)
    {
        OnlineMenuMod.Instance?.OnLocalPlayerPossessed(__instance, __0, __2);
    }
}

/// <summary>
/// PlayerRacingController derives the human index it feeds to MenuEventsManager from
/// <c>PlayerInput.user.index</c>, which is the InputUser's position in <c>InputUser.all</c> and has nothing to
/// do with <c>PlayerInput.playerIndex</c>. A client whose slot is not 0 is still the only local user, so its
/// input arrives as index 0 and drives the host's box instead of its own. An online session has exactly one
/// local human, so every input-driven entry point belongs to <see cref="OnlineSelection.LocalSlot"/>.
/// Only these five are redirected; MenuEventsManager.Select/Deselect carry genuine per-slot indices.
/// </summary>
internal static class LocalHumanIndex
{
    internal static void Redirect(ref int humanIndex)
    {
        if (!OnlineSelection.IsActive)
        {
            return;
        }

        var slot = OnlineSelection.LocalSlot;
        if (slot >= 0)
        {
            humanIndex = slot;
        }
    }
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Move))]
internal static class MenuEventsManagerMovePatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Submit))]
internal static class MenuEventsManagerSubmitPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Cancel))]
internal static class MenuEventsManagerCancelPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.CancelRelease))]
internal static class MenuEventsManagerCancelReleasePatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Config))]
internal static class MenuEventsManagerConfigPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}
