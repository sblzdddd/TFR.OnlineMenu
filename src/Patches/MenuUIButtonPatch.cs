using HarmonyLib;
using Il2Cpp;
using TFROnlineMenu.Ui;

namespace TFROnlineMenu.Patches;

[HarmonyPatch(typeof(MenuUIButton.__c__DisplayClass17_0), "_Awake_b__4")]
internal static class MenuUIButtonSubmitPatch
{
    static void Prefix(MenuUIButton.__c__DisplayClass17_0 __instance)
    {
        var name = __instance.__4__this.gameObject.name;
        if (name == "OnlineButton")
        {
            OnlineRaceMenu.Enter();
        }
        else if (name == "RaceButton")
        {
            OnlineRaceMenu.RestoreLabelsIfNeeded();
        }
    }
}
