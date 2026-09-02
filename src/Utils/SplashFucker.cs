using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TFROnlineMenu.Utils;

[HarmonyPatch(typeof(SplashScript), nameof(SplashScript.Start))]
internal static class SplashFucker
{
    static LemonAction? _fuck;

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
        splash.EndSplash();
    }

    static void UnFuck()
    {
        if (_fuck is null) return;
        MelonEvents.OnUpdate.Unsubscribe(_fuck);
        _fuck = null;
    }
}
