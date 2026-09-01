using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace TFROnlineMenu;

[HarmonyPatch(typeof(SplashScript), nameof(SplashScript.Start))]
internal static class SplashFucker
{
    static LemonAction? _fuck;

    [HarmonyPatch(typeof(SplashScreen), nameof(SplashScreen.Draw))]
    internal static class UnitySplashDrawPatch
    {
        static bool Prefix()
        {
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
            return false;
        }
    }

    static void Postfix(SplashScript __instance)
    {
        if (Application.isBatchMode)
        {
            __instance.CancelInvoke();
            __instance.EndSplash();
            return;
        }

        _fuck = () => Fuck(__instance);
        MelonEvents.OnUpdate.Subscribe(_fuck);
    }

    static void Fuck(SplashScript splash)
    {
        if (!splash) UnFuck();
        if (!Input.anyKeyDown) return;
        splash.CancelInvoke();
        SplashSucker.FuckSplash();
        splash.EndSplash();
    }

    static void UnFuck()
    {
        if (_fuck is null) return;
        MelonEvents.OnUpdate.Unsubscribe(_fuck);
        _fuck = null;
    }
}
