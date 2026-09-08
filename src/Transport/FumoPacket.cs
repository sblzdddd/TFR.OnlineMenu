namespace TFROnlineMenu.Transport;

internal static class FumoPacket
{
    internal const ushort Id = 0x54F1;

    internal enum Kind : byte
    {
        PlayerStatus = 1
    }
}
