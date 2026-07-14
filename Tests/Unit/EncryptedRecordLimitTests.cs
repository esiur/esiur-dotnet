using System.Reflection;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Cryptography;

namespace Esiur.Tests.Unit;

public class EncryptedRecordLimitTests
{
    [Fact]
    public void OversizedPlaintext_IsRejectedBeforeCipherSequenceIsConsumed()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Encryption.MaximumRecordSize = 32;
        var provider = new CountingEncryptionProvider();
        var connection = new EpConnection();
        connection.Session.EncryptionProvider = provider;
        connection.Session.SymetricCipher = provider.Cipher;

        typeof(EpConnection)
            .GetField("_serverWarehouse", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(connection, warehouse);
        var compose = typeof(EpConnection)
            .GetMethod("ComposeEncryptedRecord", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var error = Assert.Throws<TargetInvocationException>(() =>
            compose.Invoke(connection, new object[] { new byte[9] }));

        Assert.IsType<ParserLimitException>(error.InnerException);
        Assert.Equal(0, provider.Cipher.EncryptCalls);
    }

    sealed class CountingEncryptionProvider : IEncryptionProvider
    {
        public string DefaultName => "counting";
        public uint MaximumRecordOverhead => 24;
        public CountingCipher Cipher { get; } = new CountingCipher();
        public ISymetricCipher CreateCipher(EncryptionContext context) => Cipher;
    }

    sealed class CountingCipher : ISymetricCipher
    {
        public int EncryptCalls { get; private set; }
        public ushort Identifier => 0;

        public byte[] Encrypt(byte[] data)
        {
            EncryptCalls++;
            return new byte[data.Length + 24];
        }

        public byte[] Decrypt(byte[] data) => data;
        public byte[] SetKey(byte[] key) => key;
    }
}
