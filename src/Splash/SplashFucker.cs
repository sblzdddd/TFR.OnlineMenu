using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TFROnlineMenu.Utils;
using UnityEngine;

namespace TFROnlineMenu.Splash;

[HarmonyPatch(typeof(SplashScript), nameof(SplashScript.Start))]
internal static class SplashFucker
{
    private static LemonAction? _fuck;

    private static void Postfix(SplashScript __instance)
    {
        LaunchArgs.EnsureParsed();
        if (Application.isBatchMode || LaunchArgs.SkipSplash)
        {
            __instance.CancelInvoke();
            __instance.EndSplash();
            return;
        }

        _fuck = () => Fuck(__instance);
        MelonEvents.OnUpdate.Subscribe(_fuck);
    }

    private static void Fuck(SplashScript splash)
    {
        if (!splash) UnFuck();
        if (!Input.anyKeyDown) return;
        splash.CancelInvoke();
        splash.EndSplash();
    }

    private static void UnFuck()
    {
        if (_fuck is null) return;
        MelonEvents.OnUpdate.Unsubscribe(_fuck);
        _fuck = null;
    }
}
