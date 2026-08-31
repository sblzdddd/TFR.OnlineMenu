using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TFROnlineMenu;

[HarmonyPatch(typeof(SplashScript), nameof(SplashScript.Start))]
internal static class SplashSkip
{
    static LemonAction? _tick;

    static void Postfix(SplashScript __instance)
    {
        _tick = () => Tick(__instance);
        MelonEvents.OnUpdate.Subscribe(_tick);
    }

    static void Tick(SplashScript splash)
    {
        if (!splash) UnFuck();

        if (!Input.anyKeyDown) return;
        splash.CancelInvoke();
        splash.EndSplash();
    }

    static void UnFuck()
    {
        if (_tick is null) return;
        MelonEvents.OnUpdate.Unsubscribe(_tick);
        _tick = null;
    }
}
