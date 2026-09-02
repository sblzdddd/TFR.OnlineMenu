using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace TFROnlineMenu.Patches;

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
        if (boxes is null || boxes.Length == 0 || !boxes[0] || !boxes[0].ready)
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

        if (LevelManager.instance)
        {
            LevelManager.instance.LoadMainMenu();
        }

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

        if (__instance._currentModule is not null)
        {
            __instance._currentModule.End();
        }

        if (!Il2CppMirror.NetworkServer.active)
        {
            return false;
        }

        var props = new GameModeManager.SGameModeProperties
        {
            _cup = __instance._cup,
            _maps = __instance._maps,
            _itemType = 0,
            _laps = __instance._laps,
            _maxPlayers = OnlineSelection.ConnectedCount,
            _humans = GameManager.players
        };
        OnlineMenuMod.Instance.StartRaceFromSelection(props);
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
