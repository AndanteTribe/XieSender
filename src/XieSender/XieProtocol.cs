using System.Runtime.InteropServices;

namespace XieSender;

internal static class XieProtocol
{
    public const ushort XIE_MAGIC = 0x5849; // "XI"
    public const byte XIE_VERSION = 1;
    public const byte XIE_TYPE_GAMEPAD = 1;
    public const int XIE_PACKET_SIZE = 22;

    public const byte XIE_FLAG_HEARTBEAT = 0x4;

    public static byte MakeTypeFlags(byte type, byte flags)
        => (byte)((flags << 4) | (type & 0x0F));
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct XiePacket
{
    public ushort magic;
    public byte version;
    public byte typeAndFlags; // upper 4bit: flags / lower 4bit: type
    public ushort sampleId;
    public uint timestampUs;
    public short lx, ly, rx, ry;
    public byte lt, rt;
    public ushort buttons;
}