using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using TFROnlineMenu.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod : MelonMod
{
    internal static OnlineMenuMod Instance { get; private set; } = null!;

    internal string Nickname = "Fumo";
    internal string Address = "127.0.0.1";
    internal ushort Port = 7777;
    internal string Map = "forest";
    internal string Laps = "3";
    internal string Message = "Click Host or Join to begin.";

    public override void OnInitializeMelon()
    {
        Instance = this;
        LoggerInstance.Msg("TFR Online Menu initialized.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        HandleNetworkSceneLoaded(sceneName);
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (sceneName.Equals("menu2", StringComparison.OrdinalIgnoreCase))
        {
            RestoreOnlineButton();
        }
    }

    public override void OnUpdate()
    {
        UpdateRaceReady();
        OnlineRaceMenu.Tick();
        JoinPanel.Tick();
        ProfilePanel.Tick();
    }

    public override void OnApplicationQuit()
    {
        AudioListener.volume = 0.0f;
        AudioListener.pause = true;
        StopNetwork();
    }

    private void RestoreOnlineButton()
    {
        var onlineObject = GameObject.Find("OnlineButton");
        onlineObject.GetComponent<Image>().color = Color.white;
        onlineObject.transform.Find("Text").GetComponent<TextMeshProUGUI>().color = Color.white;
        var button = onlineObject.GetComponent<Button>();
        button.enabled = true;
        button.interactable = true;

        var menuButton = onlineObject.GetComponent<MenuUIButton>();
        menuButton.enabled = true;
        menuButton._transition = GameObject.Find("RaceButton").GetComponent<MenuUIButton>()._transition;

        var solo = GameObject.Find("SoloButton").GetComponent<Button>();
        var customNav = solo.navigation;
        customNav.selectOnDown = button;
        solo.navigation = customNav;
        var extra = GameObject.Find("ExtrasButton").GetComponent<Button>();
        customNav = extra.navigation;
        customNav.selectOnUp = button;
        extra.navigation = customNav;
        customNav = button.navigation;
        customNav.mode = Navigation.Mode.Explicit;
        customNav.selectOnUp = solo;
        customNav.selectOnDown = extra;
        button.navigation = customNav;
    }
}
