using Il2CppTMPro;
using TFROnlineMenu.Select;
using TFROnlineMenu.Utils;
using UnityEngine;

namespace TFROnlineMenu.Home.UI;

internal static class OnlineRaceMenu
{
    private const string HostLabel = "Host";
    private const string JoinLabel = "Join";
    private const string ProfileLabel = "Profile";

    private static readonly string[] OriginalLabels = ["Grand Prix", "Quick Race", "Custom"];

    internal static bool Active { get; private set; }
    internal static bool HostStarted { get; private set; }

    internal static bool HasSession =>
        Il2CppMirror.NetworkServer.active || Il2CppMirror.NetworkClient.active;

    internal static void Enter()
    {
        Active = true;
        HostStarted = false;
        if (HasSession && !Il2CppMirror.NetworkServer.active)
        {
            ApplyOriginalLabels();
            OnlineSelection.RequestFollow();
            return;
        }

        ApplyOnlineLabels();
        if (LaunchArgs.Auto == LaunchAutoMode.Host) OnHost();
    }

    internal static void Exit()
    {
        CreditsPopup.Close();
        if (GameObject.Find("RaceMenu"))
        {
            ApplyOriginalLabels();
        }

        if (HostStarted && Il2CppMirror.NetworkServer.active && !Il2Cpp.GameManager.inRace)
        {
            OnlineMenuMod.Instance.StopNetwork();
        }

        HostStarted = false;
        Active = false;
    }

    internal static void Suspend()
    {
        CreditsPopup.Close();
        HostStarted = false;
        Active = false;
    }

    internal static void RestoreLabelsIfNeeded()
    {
        if (!Active)
        {
            if (GameObject.Find("RaceMenu"))
            {
                ApplyOriginalLabels();
            }

            return;
        }

        Exit();
    }

    internal static void Tick()
    {
        if (!Active) return;

        if (!GameObject.Find("RaceMenu"))
        {
            if (!OnlineSelection.IsActive)
            {
                Suspend();
            }

            return;
        }

        if (HasSession)
        {
            ApplyOriginalLabels();
            return;
        }

        ApplyOnlineLabels();
    }

    internal static void OnHost()
    {
        if (HostStarted || HasSession)
        {
            return;
        }

        OnlineMenuMod.Instance.StartHost();
        if (!HasSession)
        {
            return;
        }

        HostStarted = true;
        ApplyOriginalLabels();
    }

    internal static void OnJoin()
    {
        if (HasSession)
        {
            return;
        }

        JoinPanel.Open();
    }

    internal static void OnProfile()
    {
        ProfilePanel.Open();
    }

    private static void ApplyOnlineLabels()
    {
        SetButtonText(HostButton(), HostLabel);
        SetButtonText(JoinButton(), JoinLabel);
        SetButtonText(ProfileButton(), ProfileLabel);
    }

    private static void ApplyOriginalLabels()
    {
        SetButtonText(HostButton(), OriginalLabels[0]);
        SetButtonText(JoinButton(), OriginalLabels[1]);
        SetButtonText(ProfileButton(), OriginalLabels[2]);
    }

    internal static GameObject HostButton() => Option("GrandPrixButton");
    internal static GameObject JoinButton() => Option("QuickRaceButton");
    internal static GameObject ProfileButton() => Option("CustomButton");

    private static GameObject Option(string name)
    {
        var race = GameObject.Find("RaceMenu") ?? SceneObjects.Find("RaceMenu");
        return race.transform.Find("OptionsRoot/OptionsPivot").Find(name).gameObject;
    }

    private static void SetButtonText(GameObject button, string text)
    {
        button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;
    }
}
