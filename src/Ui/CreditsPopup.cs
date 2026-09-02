using Il2Cpp;
using Il2CppTMPro;
using TFROnlineMenu.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TFROnlineMenu.Ui;

internal static class CreditsPopup
{
    static GameObject? _root;
    static GameObject? _returnTo;
    static Action? _onConfirm;
    static Action? _onCancel;

    internal static bool IsOpen => _root;

    internal static GameObject Open(string title, GameObject returnTo, Action onConfirm, Action onCancel, bool bindMenuActions = true)
    {
        Close();
        _returnTo = returnTo;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        var raceMenu = GameObject.Find("RaceMenu").transform;
        var canvas = raceMenu.GetComponentInParent<Canvas>().transform;
        _root = UnityEngine.Object.Instantiate(raceMenu.parent.Find("Credits").Find("bg").gameObject, canvas, false);
        _root.name = "OnlineCreditsPopup";
        _root.SetActive(true);
        var rect = _root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(980f, 680f);
        rect.SetAsLastSibling();

        while (_root.transform.childCount > 0)
        {
            UnityEngine.Object.DestroyImmediate(_root.transform.GetChild(0).gameObject);
        }

        AddLabel("PopupTitle", title, new Vector2(0f, 220f), 56f);
        if (bindMenuActions)
        {
            BindSubmitCancel();
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
            UnityEngine.Object.Destroy(_root);
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

    static void BindSubmitCancel()
    {
        var button = _root!.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener((UnityAction)OnConfirm);

        var trigger = _root.AddComponent<EventTrigger>();
        var cancel = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
        cancel.callback.AddListener((UnityAction<BaseEventData>)(_ => OnCancel()));
        trigger.triggers.Add(cancel);
    }

    static void SwallowMenuActions()
    {
        var trigger = _root!.AddComponent<EventTrigger>();
        foreach (var id in new[] { EventTriggerType.Submit, EventTriggerType.Cancel })
        {
            var entry = new EventTrigger.Entry { eventID = id };
            trigger.triggers.Add(entry);
        }
    }

    static void OnConfirm()
    {
        var confirm = _onConfirm;
        confirm?.Invoke();
        Close();
    }

    static void OnCancel()
    {
        var cancel = _onCancel;
        cancel?.Invoke();
        Close();
    }

    static void AddLabel(string name, string text, Vector2 pos, float size)
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
