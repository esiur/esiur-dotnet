using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Protocol;
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

    [Fact]
    public void TruMetadataParser_EnforcesConfiguredDepthInSyncParsing()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Parser.MaximumTypeMetadataDepth = 3;

        var accepted = ParsedTdu.ParseSync(
            ComposeTypedTdu(ComposeNestedTypeMetadata(3)),
            0,
            5,
            warehouse);

        Assert.Equal(TduClass.Typed, accepted.Class);
        Assert.Throws<ParserLimitException>(() => ParsedTdu.ParseSync(
            ComposeTypedTdu(ComposeNestedTypeMetadata(4)),
            0,
            6,
            warehouse));
    }

    [Fact]
    public async Task TruMetadataParser_EnforcesConnectionWarehouseDepthInAsyncParsing()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Parser.MaximumTypeMetadataDepth = 2;
        var connection = new EpConnection();
        await connection.Handle(
            ResourceOperation.Configure,
            new EpServerConnectionContext
            {
                Server = new EpServer(),
                Warehouse = warehouse,
            });

        try
        {
            var data = ComposeTypedTdu(ComposeNestedTypeMetadata(3));

            await Assert.ThrowsAsync<ParserLimitException>(async () =>
                await ParsedTdu.ParseAsync(data, connection));
        }
        finally
        {
            connection.Destroy();
        }
    }

    private static byte[] ComposeNestedTypeMetadata(int depth)
    {
        if (depth < 1)
            throw new ArgumentOutOfRangeException(nameof(depth));

        var metadata = new byte[depth];
        Array.Fill(metadata, (byte)TruIdentifier.TypedList, 0, depth - 1);
        metadata[^1] = (byte)TruIdentifier.Bool;
        return metadata;
    }

    private static byte[] ComposeTypedTdu(byte[] metadata)
    {
        if (metadata.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(metadata));

        var data = new byte[metadata.Length + 2];
        data[0] = 0x88; // Typed TDU with a one-byte payload-length prefix.
        data[1] = (byte)metadata.Length;
        Buffer.BlockCopy(metadata, 0, data, 2, metadata.Length);
        return data;
    }
}
