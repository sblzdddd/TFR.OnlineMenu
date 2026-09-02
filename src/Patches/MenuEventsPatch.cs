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
            if (Il2CppMirror.NetworkServer.active)
            {
                MelonLogger.Msg("[Online] Grand Prix is not implemented.");
            }
            else
            {
                OnlineSelection.RequestFollow();
            }

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
            if (Il2CppMirror.NetworkServer.active)
            {
                OnlineSelection.BeginFromHost();
            }
            else
            {
                OnlineSelection.RequestFollow();
            }

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
            if (Il2CppMirror.NetworkServer.active)
            {
                MelonLogger.Msg("[Online] Custom Race is not implemented.");
            }
            else
            {
                OnlineSelection.RequestFollow();
            }

            return false;
        }

        OnlineRaceMenu.OnProfile();
        return false;
    }
}
