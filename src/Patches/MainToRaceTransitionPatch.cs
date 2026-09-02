using HarmonyLib;
using Il2Cpp;
using TFROnlineMenu.Ui;
using UnityEngine.EventSystems;

namespace TFROnlineMenu.Patches;

[HarmonyPatch(typeof(MainToRaceTransition), nameof(MainToRaceTransition.BeginSequence))]
internal static class MainToRaceBeginPatch
{
    static void Prefix()
    {
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected.name == "OnlineButton")
        {
            OnlineRaceMenu.Enter();
            return;
        }

        if (selected.name == "RaceButton")
        {
            OnlineRaceMenu.RestoreLabelsIfNeeded();
        }
    }
}

[HarmonyPatch(typeof(MainToRaceTransition), nameof(MainToRaceTransition.ReverseBeginSequence))]
internal static class MainToRaceReversePatch
{
    static void Postfix()
    {
        if (OnlineRaceMenu.Active)
        {
            OnlineRaceMenu.Exit();
        }
    }
}
