using Il2CppTMPro;
using TFROnlineMenu.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TFROnlineMenu.Home.UI;

internal sealed class IpAddressDial
{
    private static readonly int[] DigitSlots = [0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 16, 17, 18, 19, 20];
    private const string DefaultDisplay = "127.000.000.001:07777";
    private const float FirstRepeat = 0.28f;
    private const float HoldRepeat = 0.08f;
    private const float StickGate = 0.55f;

    private readonly TextMeshProUGUI _text;
    private string _display = DefaultDisplay;
    private int _caret;
    private float _repeatAt;
    private int _heldAxis;

    internal IpAddressDial(Transform parent)
    {
        var go = new GameObject("IpDial");
        go.transform.SetParent(parent, false);
        _text = go.AddComponent<TextMeshProUGUI>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820f, 80f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        _text.font = ModUiUtils.LoadTmpFont("Alphakind");
        _text.fontSize = 42f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = Color.white;
        Refresh();
    }

    internal string Address
    {
        get
        {
            var parts = _display.Split(':');
            var oct = parts[0].Split('.');
            return $"{int.Parse(oct[0])}.{int.Parse(oct[1])}.{int.Parse(oct[2])}.{int.Parse(oct[3])}";
        }
    }

    internal ushort Port => ushort.Parse(_display.Split(':')[1]);

    internal void Tick()
    {
        var x = ReadAxis(true);
        var y = ReadAxis(false);
        var axis = x != 0 ? x : y * 10;
        if (axis == 0)
        {
            _heldAxis = 0;
            return;
        }

        if (axis != _heldAxis)
        {
            _heldAxis = axis;
            Apply(x, y);
            _repeatAt = Time.unscaledTime + FirstRepeat;
            return;
        }

        if (!(Time.unscaledTime >= _repeatAt)) return;
        Apply(x, y);
        _repeatAt = Time.unscaledTime + HoldRepeat;
    }

    private void Apply(int x, int y)
    {
        if (x != 0)
        {
            _caret = (_caret + x + DigitSlots.Length) % DigitSlots.Length;
            Refresh();
            return;
        }

        var chars = _display.ToCharArray();
        var i = DigitSlots[_caret];
        var digit = chars[i] - '0' + y;
        chars[i] = (char)('0' + ((digit % 10) + 10) % 10);
        _display = new string(chars);
        Refresh();
    }

    private void Refresh()
    {
        var i = DigitSlots[_caret];
        _text.text = $"{_display[..i]}<color=#FFB600>{_display[i]}</color>{_display[(i + 1)..]}";
    }

    private static int ReadAxis(bool horizontal)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (horizontal)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) return -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) return 1;
            }
            else
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) return 1;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) return -1;
            }
        }

        var pad = Gamepad.current;
        if (pad == null)
        {
            return 0;
        }

        var stick = horizontal ? pad.leftStick.x.ReadValue() : pad.leftStick.y.ReadValue();
        if (stick > StickGate) return 1;
        if (stick < -StickGate) return -1;
        if (horizontal)
        {
            if (pad.dpad.left.isPressed) return -1;
            if (pad.dpad.right.isPressed) return 1;
        }
        else
        {
            if (pad.dpad.up.isPressed) return 1;
            if (pad.dpad.down.isPressed) return -1;
        }

        return 0;
    }
}
