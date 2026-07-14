using Esiur.Resource;
using Esiur.Security.Cryptography;

namespace Esiur.Tests.Unit;

public class EncryptionProviderRegistryTests
{
    [Fact]
    public void Warehouse_RegistersAndResolvesEncryptionProvidersByName()
    {
        var warehouse = new Warehouse();
        var provider = new AesEncryptionProvider();

        warehouse.RegisterEncryptionProvider(provider);

        Assert.Same(provider, warehouse.GetEncryptionProvider(AesEncryptionProvider.Name));
        Assert.Same(provider, warehouse.TryGetEncryptionProvider(AesEncryptionProvider.Name));
        Assert.Contains(AesEncryptionProvider.Name, warehouse.GetEncryptionProviderNames());
        Assert.Throws<InvalidOperationException>(() =>
            warehouse.RegisterEncryptionProvider(new AesEncryptionProvider()));
    }
}
