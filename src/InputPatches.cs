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
