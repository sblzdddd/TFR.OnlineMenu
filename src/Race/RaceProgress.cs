using Il2Cpp;
using MelonLoader;
using TFROnlineMenu.Select;
using TFROnlineMenu.Transport;
using UnityEngine;

namespace TFROnlineMenu.Race;

internal static class RaceProgress
{
    private const float PollInterval = 0.25f;
    private const float ResendInterval = 0.5f;

    private static bool _localFinished;
    private static bool _forcedEnd;
    private static float _nextPoll;
    private static float _nextResend;
    private static MelonLogger.Instance LoggerInstance => OnlineMenuMod.Instance.LoggerInstance;

    internal static void BeginRace()
    {
        _localFinished = false;
        _forcedEnd = false;
        _nextPoll = 0;
        _nextResend = 0;
        if (OnlineSelection.IsOnlineSession)
        {
            PlayerStatusSync.PushLocalFinish(false);
        }
    }

    internal static void Stop()
    {
        _localFinished = false;
        _forcedEnd = false;
        _nextPoll = 0;
        _nextResend = 0;
    }

    internal static void NotifyRacerEnded(PlayerRacer? racer)
    {
        if (!racer || _localFinished || !OnlineSelection.IsOnlineSession)
        {
            return;
        }

        var local = LocalRacer();
        if (!local || local != racer)
        {
            return;
        }

        _localFinished = true;
        _nextResend = 0;
    }

    internal static void Tick()
    {
        if (!OnlineSelection.IsOnlineSession || !GameManager.inRace)
        {
            return;
        }

        if (_localFinished)
        {
            PublishFinish();
            return;
        }

        if (_forcedEnd || Time.unscaledTime < _nextPoll)
        {
            return;
        }

        _nextPoll = Time.unscaledTime + PollInterval;
        if (!EveryPeerFinished())
        {
            return;
        }

        var racer = LocalRacer();
        if (!racer || racer!.endedRace)
        {
            return;
        }

        _forcedEnd = true;
        LoggerInstance.Msg("Every other racer finished; ending the local race so results can open.");
        racer.EndRace();
    }
    
    private static void PublishFinish()
    {
        if (Time.unscaledTime < _nextResend || !FRNetworkPlayer.localPlayer)
        {
            return;
        }

        _nextResend = Time.unscaledTime + ResendInterval;
        PlayerStatusSync.PushLocalFinish(true);
    }

    private static bool EveryPeerFinished()
    {
        var local = FRNetworkPlayer.localPlayer;
        var peers = 0;
        var finished = 0;
        foreach (var player in SelectionRoster.GetPlayers())
        {
            if (!player || player == local)
            {
                continue;
            }

            peers++;
            if (PlayerStatusSync.IsFinished(player))
            {
                finished++;
            }
        }

        return peers > 0 && finished >= peers;
    }

    private static PlayerRacer? LocalRacer()
    {
        var humans = GameManager.players;
        if (humans is null)
        {
            return null;
        }

        for (var index = 0; index < humans.Length; index++)
        {
            var human = humans[index];
            if (human is not null && human.racer)
            {
                return human.racer;
            }
        }

        return null;
    }
}
