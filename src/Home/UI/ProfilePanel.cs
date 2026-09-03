using Il2CppTMPro;
using TFROnlineMenu.Utils;
using UnityEngine;

namespace TFROnlineMenu.Home.UI;

internal static class ProfilePanel
{
    private static TextMeshProUGUI? _field;

    internal static void Open()
    {
        var root = CreditsPopup.Open("PROFILE", OnlineRaceMenu.ProfileButton(), Confirm, Release, bindMenuActions: false);
        var go = new GameObject("UsernameField");
        go.transform.SetParent(root.transform, false);
        _field = go.AddComponent<TextMeshProUGUI>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820f, 80f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        _field.font = ModUiUtils.LoadTmpFont("Alphakind");
        _field.fontSize = 42f;
        _field.alignment = TextAlignmentOptions.Center;
        _field.color = Color.white;
        _field.text = OnlineMenuMod.Instance.Nickname;
    }

    internal static void Tick()
    {
        if (_field is null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            CreditsPopup.Confirm();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CreditsPopup.Cancel();
            return;
        }

        foreach (var c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_field.text.Length > 0)
                {
                    _field.text = _field.text[..^1];
                }

                continue;
            }

            if (c < 32 || _field.text.Length >= 63)
            {
                continue;
            }

            _field.text += c;
        }
    }

    private static void Confirm()
    {
        OnlineMenuMod.Instance.Nickname = _field!.text;
        Release();
    }

    internal static void Release()
    {
        _field = null;
    }
}
