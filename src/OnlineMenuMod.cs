using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod : MelonMod
{
    internal static OnlineMenuMod? Instance { get; private set; }

    private Button? _onlineButton;
    private UnityAction? _onlineButtonAction;
    private bool _showPanel;
    private string _nickname = "Fumo";
    private string _address = "127.0.0.1";
    private string _map = "forest";
    private string _laps = "3";
    private string _message = "Click Host or Join to begin.";

    public override void OnInitializeMelon()
    {
        Instance = this;
        LoggerInstance.Msg("TFR Online Menu initialized. Press F8 if the Online button is unavailable.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        _onlineButton = null;
        _onlineButtonAction = null;
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
        if (Input.GetKeyDown(KeyCode.F8))
        {
            TogglePanel();
        }

        UpdateRaceReady();
    }

    public override void OnGUI()
    {
        if (!_showPanel)
        {
            return;
        }

        var scale = Mathf.Max(1, Screen.height / 1080f);
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

        var panel = new Rect(
            (Screen.width / scale - 600) / 2,
            (Screen.height / scale - 420) / 2,
            600,
            420);
        DrawOnlinePanel(panel);
        GUI.matrix = previousMatrix;
    }

    private void DrawOnlinePanel(Rect panel)
    {
        GUI.Box(panel, "TFR Multiplayer Prototype");

        var x = panel.x + 24;
        var y = panel.y + 42;
        var width = panel.width - 48;
        DrawField(ref y, x, width, "Nickname", ref _nickname, 63);
        DrawField(ref y, x, width, "Host address", ref _address, 255);
        DrawField(ref y, x, width, "Map", ref _map, 64);
        DrawField(ref y, x, width, "Laps", ref _laps, 2);

        var online = Il2CppMirror.NetworkServer.active || Il2CppMirror.NetworkClient.active;
        if (!online)
        {
            var buttonWidth = (width - 12) / 2;
            if (GUI.Button(new Rect(x, y, buttonWidth, 36), "Host")) StartHost();
            if (GUI.Button(new Rect(x + buttonWidth + 12, y, buttonWidth, 36), "Join")) StartClient();
        }
        else if (GUI.Button(new Rect(x, y, width, 36), "Stop"))
        {
            StopNetwork();
        }
        y += 46;

        if (Il2CppMirror.NetworkServer.active)
        {
            if (GUI.Button(new Rect(x, y, width, 36), "Start Race")) StartRace();
            y += 46;
        }

        GUI.Label(new Rect(x, y, width, 24), GetNetworkStatus());
        GUI.Label(new Rect(x, y + 30, width, 54), _message);
        if (GUI.Button(new Rect(panel.x + panel.width - 48, panel.y + 8, 32, 24), "X")) _showPanel = false;
    }

    private static void DrawField(ref float y, float x, float width, string label, ref string value, int maxLength)
    {
        GUI.Label(new Rect(x, y, 120, 28), label);
        value = GUI.TextField(new Rect(x + 120, y, width - 120, 28), value, maxLength);
        y += 36;
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

        _onlineButtonAction = (UnityAction)TogglePanel;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(_onlineButtonAction);
        _onlineButton = button;
    }

    private void TogglePanel()
    {
        _showPanel = !_showPanel;
    }
}
