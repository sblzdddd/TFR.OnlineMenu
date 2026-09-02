using HarmonyLib;
using Il2Cpp;
using TFROnlineMenu.Ui;

namespace TFROnlineMenu.Patches;

[HarmonyPatch(typeof(MenuEvents), nameof(MenuEvents.GrandPrix))]
internal static class MenuEventsGrandPrixPatch
{
    static bool Prefix()
    {
        if (CreditsPopup.IsOpen)
        {
            return false;
        }

        if (!OnlineRaceMenu.Active)
        {
            return true;
        }

        OnlineRaceMenu.OnHost();
        return false;
    }
}

[HarmonyPatch(typeof(MenuEvents), nameof(MenuEvents.QuickRace))]
internal static class MenuEventsQuickRacePatch
{
    static bool Prefix()
    {
        if (CreditsPopup.IsOpen)
        {
            return false;
        }

        if (!OnlineRaceMenu.Active)
        {
            return true;
        }

        OnlineRaceMenu.OnJoin();
        return false;
    }
}

[HarmonyPatch(typeof(MenuEvents), nameof(MenuEvents.GoCustomRace))]
internal static class MenuEventsGoCustomRacePatch
{
    static bool Prefix()
    {
        if (CreditsPopup.IsOpen)
        {
            return false;
        }

        if (!OnlineRaceMenu.Active)
        {
            return true;
        }

        OnlineRaceMenu.OnProfile();
        return false;
    }
}
