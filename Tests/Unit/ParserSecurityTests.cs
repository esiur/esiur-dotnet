using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class ParserSecurityTests
{
    [Fact]
    public void PacketParser_RejectsOversizedDeclarationBeforePayloadArrives()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Parser.MaximumPacketSize = 1_024;
        var packet = new EpPacket(warehouse);

        // EP notification with a RawData TDU whose four-byte length declares 1 MiB,
        // without supplying that payload. The declaration itself must be rejected.
        var data = new byte[] { 0x20, 0x60, 0x00, 0x10, 0x00, 0x00 };

        Assert.Throws<ParserLimitException>(() =>
        {
            packet.Parse(data, 0, (uint)data.Length);
        });
    }

    [Fact]
    public void RawDataParser_EnforcesAllocationBudget()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Parser.MaximumAllocationSize = 10;
        var data = Codec.Compose(new byte[20], warehouse, null);

        Assert.Throws<ParserLimitException>(() => Codec.ParseSync(data, 0, warehouse));
    }

    [Fact]
    public void StringParser_AccountsForDecodedUtf16Allocation()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Parser.MaximumAllocationSize = 10;
        var data = Codec.Compose("123456", warehouse, null);

        Assert.Throws<ParserLimitException>(() => Codec.ParseSync(data, 0, warehouse));
    }

    [Fact]
    public void ListParser_EnforcesCollectionItemBudget()
    {
        var warehouse = new Warehouse();
        var data = Codec.Compose(new object[] { true, false, true }, warehouse, null);
        warehouse.Configuration.Parser.MaximumCollectionItems = 2;

        Assert.Throws<ParserLimitException>(() => Codec.ParseSync(data, 0, warehouse));
    }
}
