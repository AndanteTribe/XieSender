namespace XieSender.Tests;

public class XieProtocolTests
{
    [Fact]
    public void ConstantsShouldHaveCorrectValues()
    {
        Assert.Equal((ushort)0x5849, XieProtocol.XIE_MAGIC);
        Assert.Equal((byte)1, XieProtocol.XIE_VERSION);
        Assert.Equal((byte)1, XieProtocol.XIE_TYPE_GAMEPAD);
        Assert.Equal(22, XieProtocol.XIE_PACKET_SIZE);
        Assert.Equal((byte)0x4, XieProtocol.XIE_FLAG_HEARTBEAT);
    }

    [Theory]
    [InlineData(1, 0, 0x01)]
    [InlineData(1, 4, 0x41)]
    [InlineData(2, 8, 0x82)]
    [InlineData(15, 15, 0xFF)]
    public void MakeTypeFlagsShouldCombineCorrectly(byte type, byte flags, byte expected)
    {
        var result = XieProtocol.MakeTypeFlags(type, flags);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MakeTypeFlagsTypeOnlyShouldUseOnlyLower4Bits()
    {
        var result = XieProtocol.MakeTypeFlags(0xFF, 0);
        Assert.Equal(0x0F, result);
    }

    [Fact]
    public void MakeTypeFlagsFlagsOnlyShouldUseOnlyUpper4Bits()
    {
        var result = XieProtocol.MakeTypeFlags(0, 0xFF);
        Assert.Equal(0xF0, result);
    }

    [Fact]
    public void MakeTypeFlagsHeartbeatShouldWork()
    {
        var result = XieProtocol.MakeTypeFlags(XieProtocol.XIE_TYPE_GAMEPAD, XieProtocol.XIE_FLAG_HEARTBEAT);
        Assert.Equal(0x41, result); // 上位4bit: 0x4, 下位4bit: 0x1
    }

    [Fact]
    public void PacketSizeShouldBe22Bytes()
    {
        // XiePacket 構造体のサイズを確認
        unsafe
        {
            var size = sizeof(XiePacket);
            Assert.Equal(22, size);
        }
    }
}



