using HarmonyLib;
using Il2Cpp;
using Il2CppMirror;
using MelonLoader;

namespace TFROnlineMenu.Patches;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OnSceneLoaded))]
internal static class MainMenuOnSceneLoadedPatch
{
    static bool Prefix(MainMenuManager __instance)
    {
        if (__instance._startModule is null)
        {
            OnlineSelection.InstallSelectionSequence(__instance);
        }

        if (__instance._startModule is not null)
        {
            return true;
        }

        MelonLogger.Msg("[Online] Skipping selection LoadSelection until a module is ready.");
        return false;
    }

    static void Postfix()
    {
        if (OnlineSelection.IsOnlineSession)
        {
            OnlineSelection.HandleSelectionUiReady();
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), "CheckForPlayers")]
internal static class CharacterSelectionCheckForPlayersPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.ShouldStayInSession;
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.RefreshMatchingAll))]
internal static class CharacterSelectionRefreshMatchingAllPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.ShouldStayInSession;
    }
}

[HarmonyPatch(typeof(RacingInputManager), nameof(RacingInputManager.OnPlayerLeft))]
internal static class RacingInputManagerOnPlayerLeftPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.ShouldStayInSession;
    }
}

[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.ClientChangeScene))]
internal static class MirrorClientChangeScenePatch
{
    static bool Prefix(string newSceneName)
    {
        if (string.IsNullOrEmpty(newSceneName) ||
            !newSceneName.Equals(OnlineSelection.SelectionScene, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        MelonLogger.Msg("[Online] Ignoring Mirror selection scene load; following via LevelManager.");
        OnlineSelection.OnSelectionInvite();
        return false;
    }
}

[HarmonyPatch(typeof(LevelManager), nameof(LevelManager.LoadMainMenu))]
internal static class LevelManagerLoadMainMenuPatch
{
    static bool Prefix()
    {
        if (!OnlineSelection.ShouldStayInSession)
        {
            return true;
        }

        MelonLogger.Msg("[Online] Ignoring LoadMainMenu while the online selection session is active.");
        return false;
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.EnableJoining))]
internal static class CharacterSelectionEnableJoiningPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.IsActive;
    }
}

[HarmonyPatch(typeof(CharacterSelectionBehaviour), nameof(CharacterSelectionBehaviour.Update))]
internal static class CharacterSelectionUpdatePatch
{
    static bool Prefix(CharacterSelectionBehaviour __instance)
    {
        if (!OnlineSelection.IsActive)
        {
            return true;
        }

        if (!__instance.enabled)
        {
            return false;
        }

        if (!Il2CppMirror.NetworkServer.active)
        {
            return false;
        }

        var boxes = __instance._boxes;
        var slot = OnlineSelection.LocalSlot;
        if (boxes is null || slot < 0 || slot >= boxes.Length || !boxes[slot] || !boxes[slot].ready)
        {
            return false;
        }

        var menu = UnityEngine.Object.FindObjectOfType<SelectionMenuBehaviour>();
        if (menu)
        {
            menu.NextModule();
        }

        __instance.enabled = false;
        return false;
    }
}

[HarmonyPatch(typeof(CupSelectionBehaviour), nameof(CupSelectionBehaviour.Proceed))]
internal static class CupSelectionProceedPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.IsActive || Il2CppMirror.NetworkServer.active;
    }
}

[HarmonyPatch(typeof(CupSelectionBehaviour), nameof(CupSelectionBehaviour.FinishSelection))]
internal static class CupSelectionFinishPatch
{
    static bool Prefix()
    {
        return !OnlineSelection.IsActive || Il2CppMirror.NetworkServer.active;
    }
}

[HarmonyPatch(typeof(SelectionMenuBehaviour), nameof(SelectionMenuBehaviour.PrevModule))]
internal static class SelectionPrevModulePatch
{
    static bool Prefix(SelectionMenuBehaviour __instance)
    {
        if (!OnlineSelection.IsActive)
        {
            return true;
        }

        if (__instance._currentModule is not null && __instance._currentModule.prev is not null)
        {
            return true;
        }

        OnlineSelection.LeaveSession("Left the online session.");
        return false;
    }
}

[HarmonyPatch(typeof(SelectionMenuBehaviour), nameof(SelectionMenuBehaviour.NextModule))]
internal static class SelectionNextModulePatch
{
    static bool Prefix(SelectionMenuBehaviour __instance)
    {
        if (!OnlineSelection.IsActive)
        {
            return true;
        }

        if (__instance._currentModule is not null && __instance._currentModule.next is not null)
        {
            return true;
        }

        OnlineSelection.FinishFromMenu(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(SelectionMenuBehaviour), "EndSelection")]
internal static class SelectionEndSelectionPatch
{
    static bool Prefix(SelectionMenuBehaviour __instance)
    {
        if (!OnlineSelection.IsActive)
        {
            return true;
        }

        OnlineSelection.FinishFromMenu(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CreateAIPlayer))]
internal static class CreateAIPlayerPatch
{
    static bool Prefix(ref AIGamePlayer __result)
    {
        if (!OnlineSelection.IsOnlineSession)
        {
            return true;
        }

        MelonLogger.Msg("[Online] Skipping AI fill.");
        __result = GameManager.CreateNullAIPlayer();
        __result.nulled = true;
        return false;
    }
}

[HarmonyPatch(typeof(FRNetGameState), "OnNetGameStart")]
internal static class NetGameStartFollowPatch
{
    static void Prefix()
    {
        OnlineSelection.NotifyRaceStarting();
    }
}
