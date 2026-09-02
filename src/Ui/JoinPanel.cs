namespace TFROnlineMenu.Ui;

internal static class JoinPanel
{
    static IpAddressDial? _dial;

    internal static void Open()
    {
        var root = CreditsPopup.Open("JOIN", OnlineRaceMenu.JoinButton(), Confirm, Release);
        _dial = new IpAddressDial(root.transform);
    }

    internal static void Tick()
    {
        if (!CreditsPopup.IsOpen || _dial is null)
        {
            return;
        }

        _dial.Tick();
    }

    internal static void Release()
    {
        _dial = null;
    }

    static void Confirm()
    {
        var mod = OnlineMenuMod.Instance;
        mod.Address = _dial!.Address;
        mod.Port = _dial.Port;
        mod.StartClient();
        Release();
    }
}
