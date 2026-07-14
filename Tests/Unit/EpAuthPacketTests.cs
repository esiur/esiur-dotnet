using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;

namespace Esiur.Tests.Unit;

public class EpAuthPacketTests
{
    [Theory]
    [InlineData(AuthenticationMode.None, EncryptionMode.None)]
    [InlineData(AuthenticationMode.InitializerIdentity, EncryptionMode.None)]
    [InlineData(AuthenticationMode.InitializerIdentity, EncryptionMode.EncryptWithSessionKey)]
    [InlineData(AuthenticationMode.ResponderIdentity, EncryptionMode.EncryptWithSessionKeyAndAddress)]
    [InlineData(AuthenticationMode.DualIdentity, EncryptionMode.EncryptWithSessionKey)]
    public void InitializeHeader_KeepsAuthenticationAndEncryptionBitsIndependent(
        AuthenticationMode authenticationMode,
        EncryptionMode encryptionMode)
    {
        var encoded = (byte)((((byte)authenticationMode & 0x3) << 2)
                           | ((byte)encryptionMode & 0x3));
        var packet = new EpAuthPacket(new Warehouse());

        Assert.Equal(1, packet.Parse(new[] { encoded }, 0, 1));
        Assert.Equal(EpAuthPacketCommand.Initialize, packet.Command);
        Assert.Equal(authenticationMode, packet.AuthMode);
        Assert.Equal(encryptionMode, packet.EncryptionMode);
    }
}
