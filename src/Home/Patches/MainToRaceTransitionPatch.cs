using HarmonyLib;
using Il2Cpp;
using TFROnlineMenu.Home.UI;
using UnityEngine.EventSystems;

namespace TFROnlineMenu.Home.Patches;

[HarmonyPatch(typeof(MainToRaceTransition), nameof(MainToRaceTransition.BeginSequence))]
internal static class MainToRaceBeginPatch
{
    private static void Prefix()
    {
        var selected = EventSystem.current.currentSelectedGameObject ?? null;
        if (selected is null) return;

        if (selected.name == "OnlineButton")
        {
            OnlineRaceMenu.Enter();
        }
        else if (selected.name == "RaceButton")
        {
            OnlineRaceMenu.RestoreLabelsIfNeeded();
        }
    }
}

[HarmonyPatch(typeof(MainToRaceTransition), nameof(MainToRaceTransition.ReverseBeginSequence))]
internal static class MainToRaceReversePatch
{
    private static void Postfix()
    {
        if (OnlineRaceMenu.Active)
            OnlineRaceMenu.Exit();
    }
}
