using HarmonyLib;
using Il2Cpp;
using MelonLoader;
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

        if (OnlineRaceMenu.HasSession)
        {
            MelonLogger.Msg("[Online] Grand Prix is not implemented.");
            return false;
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

        if (OnlineRaceMenu.HasSession)
        {
            OnlineSelection.BeginFromHost();
            return false;
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

        if (OnlineRaceMenu.HasSession)
        {
            MelonLogger.Msg("[Online] Custom Race is not implemented.");
            return false;
        }

        OnlineRaceMenu.OnProfile();
        return false;
    }
}
