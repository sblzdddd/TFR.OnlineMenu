using HarmonyLib;
using Il2Cpp;

namespace TFROnlineMenu.Patches;

/// <summary>
/// <c>FRNetGameState.OnNetGameStart</c> discards the roster the server sent, rebuilds one from the spawned
/// <c>FRNetworkPlayer</c>s and then hardcodes <c>_character = 6</c> on every entry while leaving skin and
/// vehicle at zero, so everyone spawns as Marisa on the default kart. It runs on the host too, because the
/// host is also a Mirror client. <c>GameManager.StartGame</c> only stashes the settings and starts the
/// circuit load, so the picks can be written back onto the shared <c>GamePlayer</c> instances afterwards and
/// still be in place when <c>SpawnAPlayer</c> reads them.
/// </summary>
[HarmonyPatch(typeof(FRNetGameState), nameof(FRNetGameState.OnNetGameStart))]
internal static class NetGameRosterPatch
{
    static void Postfix()
    {
        // The handler is a no-op unless a client is running, and lastSettings would still hold an
        // unrelated offline race in that case.
        if (!Il2CppMirror.NetworkClient.active)
        {
            return;
        }

        var roster = GameManager.lastSettings._players;
        if (roster is null)
        {
            return;
        }

        for (var index = 0; index < roster.Length; index++)
        {
            var entry = roster[index];
            if (entry is null)
            {
                continue;
            }

            // Only the local player keeps its HumanGamePlayer; every peer is wrapped in a GameMPPlayer.
            var mp = entry.TryCast<GameMPPlayer>();
            var netPlayer = mp is not null ? mp._plr : FRNetworkPlayer.localPlayer;
            if (!netPlayer)
            {
                continue;
            }

            var info = netPlayer.Network_info._racerInfo;
            RacerInfoSync.DecodePick(info._character, info._skin, info._vehicle,
                out var character, out var skin, out var vehicle, out _);
            entry.character = Math.Max(character, 0);
            entry.skin = Math.Max(skin, 0);
            entry.vehicle = Math.Max(vehicle, 0);
        }
    }
}

[HarmonyPatch(typeof(PlayerRacer), nameof(PlayerRacer.EndRace))]
internal static class PlayerRacerEndRacePatch
{
    static void Postfix(PlayerRacer __instance)
    {
        RaceProgress.NotifyRacerEnded(__instance);
    }
}
