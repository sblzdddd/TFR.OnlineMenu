using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TFROnlineMenu.Ui;

internal static class OnlineRaceMenu
{
    const string HostLabel = "Host";
    const string StartRaceLabel = "Start Race";
    const string JoinLabel = "Join";
    const string ProfileLabel = "Profile";
    static readonly Color EnabledColor = Color.white;
    static readonly Color DisabledColor = new(0.57f, 0.57f, 0.57f, 1f);

    static readonly string[] OriginalLabels = ["Grand Prix", "Quick Race", "Custom"];

    internal static bool Active { get; private set; }
    internal static bool HostStarted { get; private set; }

    static bool CanStartRace =>
        HostStarted && FRNetworkServer.instance.GetPlayers().Count >= 2;

    internal static void Enter()
    {
        Active = true;
        HostStarted = false;
        ApplyOnlineLabels();
        RefreshButtons();
    }

    internal static void Exit()
    {
        CreditsPopup.Close();
        if (GameObject.Find("RaceMenu"))
        {
            ApplyOriginalLabels();
        }

        if (HostStarted && Il2CppMirror.NetworkServer.active && !GameManager.inRace)
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
            Suspend();
            return;
        }

        ApplyOnlineLabels();
        RefreshButtons();
    }

    internal static void OnHost()
    {
        if (HostStarted)
        {
            if (!CanStartRace)
            {
                return;
            }

            OnlineMenuMod.Instance.StartRace();
            Suspend();
            return;
        }

        OnlineMenuMod.Instance.StartHost();
        HostStarted = true;
        RefreshButtons();
    }

    internal static void OnJoin()
    {
        if (HostStarted)
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
        SetButtonText(HostButton(), HostStarted ? StartRaceLabel : HostLabel);
        SetButtonText(JoinButton(), JoinLabel);
        SetButtonText(ProfileButton(), ProfileLabel);
    }

    static void ApplyOriginalLabels()
    {
        SetButtonText(HostButton(), OriginalLabels[0]);
        SetButtonText(JoinButton(), OriginalLabels[1]);
        SetButtonText(ProfileButton(), OriginalLabels[2]);
        SetVisual(HostButton(), true);
        SetVisual(JoinButton(), true);
        SetVisual(ProfileButton(), true);
        RestoreNav();
    }

    static void RefreshButtons()
    {
        var hostOn = !HostStarted || CanStartRace;
        var joinOn = !HostStarted;
        SetVisual(HostButton(), hostOn);
        SetVisual(JoinButton(), joinOn);
        SetVisual(ProfileButton(), true);
        WireNav(hostOn, joinOn);
        if (EventSystem.current.currentSelectedGameObject)
        {
            var current = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
            if (current && !current.interactable)
            {
                MenuEventsManager.instance.SelectAtLateUpdate(FirstEnabled(hostOn, joinOn));
            }
        }
    }

    static GameObject FirstEnabled(bool hostOn, bool joinOn)
    {
        if (hostOn)
        {
            return HostButton();
        }

        if (joinOn)
        {
            return JoinButton();
        }

        return ProfileButton();
    }

    static void SetVisual(GameObject go, bool on)
    {
        var button = go.GetComponent<Button>();
        button.interactable = on;
        go.GetComponent<Image>().color = on ? EnabledColor : DisabledColor;
        go.GetComponentInChildren<TextMeshProUGUI>(true).color = on ? EnabledColor : DisabledColor;
    }

    static void WireNav(bool hostOn, bool joinOn)
    {
        var host = HostButton().GetComponent<Button>();
        var join = JoinButton().GetComponent<Button>();
        var profile = ProfileButton().GetComponent<Button>();
        var enabled = new List<Button>();
        if (hostOn) enabled.Add(host);
        if (joinOn) enabled.Add(join);
        enabled.Add(profile);

        foreach (var button in new[] { host, join, profile })
        {
            Link(button, enabled);
        }
    }

    static void RestoreNav()
    {
        var host = HostButton().GetComponent<Button>();
        var join = JoinButton().GetComponent<Button>();
        var profile = ProfileButton().GetComponent<Button>();
        Link(host, [host, join, profile]);
        Link(join, [host, join, profile]);
        Link(profile, [host, join, profile]);
    }

    static void Link(Button button, List<Button> enabled)
    {
        var index = enabled.IndexOf(button);
        var nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        if (index < 0)
        {
            nav.selectOnUp = enabled[0];
            nav.selectOnDown = enabled[0];
            button.navigation = nav;
            return;
        }

        nav.selectOnUp = enabled[(index - 1 + enabled.Count) % enabled.Count];
        nav.selectOnDown = enabled[(index + 1) % enabled.Count];
        button.navigation = nav;
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
