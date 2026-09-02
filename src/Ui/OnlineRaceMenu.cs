using Il2CppTMPro;
using UnityEngine;

namespace TFROnlineMenu.Ui;

internal static class OnlineRaceMenu
{
    const string HostLabel = "Host";
    const string JoinLabel = "Join";
    const string ProfileLabel = "Profile";

    static readonly string[] OriginalLabels = ["Grand Prix", "Quick Race", "Custom"];

    internal static bool Active { get; private set; }
    internal static bool HostStarted { get; private set; }

    internal static bool HasSession =>
        Il2CppMirror.NetworkServer.active || Il2CppMirror.NetworkClient.active;

    internal static void Enter()
    {
        Active = true;
        HostStarted = false;
        ApplyOnlineLabels();
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
        if (!Active)
        {
            return;
        }

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

    static void ApplyOnlineLabels()
    {
        SetButtonText(HostButton(), HostLabel);
        SetButtonText(JoinButton(), JoinLabel);
        SetButtonText(ProfileButton(), ProfileLabel);
    }

    static void ApplyOriginalLabels()
    {
        SetButtonText(HostButton(), OriginalLabels[0]);
        SetButtonText(JoinButton(), OriginalLabels[1]);
        SetButtonText(ProfileButton(), OriginalLabels[2]);
    }

    internal static GameObject HostButton() => Option("GrandPrixButton");
    internal static GameObject JoinButton() => Option("QuickRaceButton");
    internal static GameObject ProfileButton() => Option("CustomButton");

    static GameObject Option(string name)
    {
        var race = GameObject.Find("RaceMenu") ?? SceneObjects.Find("RaceMenu");
        return race.transform.Find("OptionsRoot/OptionsPivot").Find(name).gameObject;
    }

    static void SetButtonText(GameObject button, string text)
    {
        button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;
    }
}
