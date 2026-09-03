using Il2Cpp;
using Il2CppTMPro;
using TFROnlineMenu.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace TFROnlineMenu.Home.UI;

internal static class CreditsPopup
{
    private static GameObject? _root;
    private static GameObject? _returnTo;
    private static Action? _onConfirm;
    private static Action? _onCancel;

    internal static bool IsOpen => _root;

    internal static GameObject Open(string title, GameObject returnTo, Action onConfirm, Action onCancel, bool bindMenuActions = true)
    {
        Close();
        _returnTo = returnTo;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        // TODO: add mouse event blocker for popups
        var raceMenu = GameObject.Find("RaceMenu").transform;
        var canvas = raceMenu.GetComponentInParent<Canvas>().transform;
        _root = Instantiate(raceMenu.parent.Find("Credits").Find("bg").gameObject, canvas, false);
        _root.name = "OnlineCreditsPopup";
        _root.SetActive(true);
        var rect = _root.GetComponent<RectTransform>();
        // rect.anchorMin = new Vector2(0.5f, 0.5f);
        // rect.anchorMax = new Vector2(0.5f, 0.5f);
        // rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(680f, 250f);
        rect.SetAsLastSibling();

        while (_root.transform.childCount > 0)
        {
            DestroyImmediate(_root.transform.GetChild(0).gameObject);
        }

        AddLabel("PopupTitle", title, new Vector2(0f, 220f), 56f);
        if (bindMenuActions)
        {
            // bind confirm and cancel events
            var button = _root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            // block kb jump
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener((UnityAction)OnConfirm);

            var trigger = _root.AddComponent<EventTrigger>();
            var cancel = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
            cancel.callback.AddListener((UnityAction<BaseEventData>)(_ => OnCancel()));
            trigger.triggers.Add(cancel);
        }
        else
        {
            SwallowMenuActions();
        }

        MenuEventsManager.instance.SelectAtLateUpdate(_root);
        return _root;
    }

    internal static void Confirm() => OnConfirm();

    internal static void Cancel() => OnCancel();

    internal static void Close()
    {
        JoinPanel.Release();
        ProfilePanel.Release();
        if (_root)
        {
            Destroy(_root);
        }

        _root = null;
        _onConfirm = null;
        _onCancel = null;
        if (_returnTo)
        {
            MenuEventsManager.instance.SelectAtLateUpdate(_returnTo);
        }

        _returnTo = null;
    }

    private static void SwallowMenuActions()
    {
        var trigger = _root!.AddComponent<EventTrigger>();
        foreach (var id in new[] { EventTriggerType.Submit, EventTriggerType.Cancel })
        {
            var entry = new EventTrigger.Entry { eventID = id };
            trigger.triggers.Add(entry);
        }
    }

    private static void OnConfirm()
    {
        var confirm = _onConfirm;
        confirm?.Invoke();
        Close();
    }

    private static void OnCancel()
    {
        var cancel = _onCancel;
        cancel?.Invoke();
        Close();
    }

    private static void AddLabel(string name, string text, Vector2 pos, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root!.transform, false);
        go.AddComponent<TextMeshProUGUI>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800f, 80f);
        rect.anchoredPosition = pos;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = ModUiUtils.LoadTmpFont("Alphakind");
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;
    }
}
