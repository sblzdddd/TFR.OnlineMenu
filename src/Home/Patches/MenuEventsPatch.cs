using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TFROnlineMenu.Home.UI;
using TFROnlineMenu.Select;

namespace TFROnlineMenu.Home.Patches;

[HarmonyPatch(typeof(MenuEvents), nameof(MenuEvents.GrandPrix))]
internal static class MenuEventsGrandPrixPatch
{
    private static MelonLogger.Instance LoggerInstance => OnlineMenuMod.Instance.LoggerInstance;

    private static bool Prefix()
    {
        if (CreditsPopup.IsOpen) return false;

        if (!OnlineRaceMenu.Active) return true;

        if (OnlineRaceMenu.HasSession)
        {
            if (Il2CppMirror.NetworkServer.active)
            {
                LoggerInstance.Msg("Grand Prix is not implemented.");
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
    private static bool Prefix()
    {
        if (CreditsPopup.IsOpen) return false;

        if (!OnlineRaceMenu.Active) return true;

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
    private static MelonLogger.Instance LoggerInstance => OnlineMenuMod.Instance.LoggerInstance;

    private static bool Prefix()
    {
        if (CreditsPopup.IsOpen) return false;

        if (!OnlineRaceMenu.Active) return true;

        if (OnlineRaceMenu.HasSession)
        {
            if (Il2CppMirror.NetworkServer.active)
            {
                LoggerInstance.Msg("Custom Race is not implemented.");
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
