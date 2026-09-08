using Il2CppMirror;

namespace TFROnlineMenu.Transport.Messages;

internal struct FumoStatesMsg
{
    public uint NetId;
    public int Character;
    public int Skin;
    public int Vehicle;
    public bool Ready;
    public bool InLobby;
    public bool Finished;

    internal void Serialize(NetworkWriter writer)
    {
        writer.WriteUInt(NetId);
        writer.WriteInt(Character);
        writer.WriteInt(Skin);
        writer.WriteInt(Vehicle);
        writer.WriteBool(Ready);
        writer.WriteBool(InLobby);
        writer.WriteBool(Finished);
    }

    internal static FumoStatesMsg Deserialize(NetworkReader reader)
    {
        return new FumoStatesMsg
        {
            NetId = reader.ReadUInt(),
            Character = reader.ReadInt(),
            Skin = reader.ReadInt(),
            Vehicle = reader.ReadInt(),
            Ready = reader.ReadBool(),
            InLobby = reader.ReadBool(),
            Finished = reader.ReadBool()
        };
    }
}
