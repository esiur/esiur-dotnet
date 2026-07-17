using System.ComponentModel.DataAnnotations;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Stores.EntityCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Esiur.Tests.EntityCore;

public sealed class EntityCoreProviderTests
{
    [Fact]
    public async Task Sqlite_PersistsAndMaterializesEsiurResources()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var warehouse = new Warehouse();
        var store = await warehouse.Put("database", new EntityStore());
        DbContextOptions<ResourceDbContext>? options = null;
        options = new DbContextOptionsBuilder<ResourceDbContext>()
            .UseSqlite(connection)
            .UseEsiur(store, () => new ResourceDbContext(options!))
            .Options;

        try
        {
            await warehouse.Open();

            await using var context = new ResourceDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var added = await context.Resources.AddResourceAsync(new ProviderResource
            {
                Name = "SQLite resource",
            });

            Assert.True(added.Id > 0);
            Assert.NotNull(added.Instance);
            Assert.Same(store, added.Instance!.Store);

            context.ChangeTracker.Clear();

            var loaded = await context.Resources.SingleAsync(x => x.Id == added.Id);

            Assert.Same(added, loaded);
            Assert.Equal("SQLite resource", loaded.Name);
            Assert.NotNull(await warehouse.Get<IResource>($"database/{nameof(ProviderResource)}/{added.Id}"));
        }
        finally
        {
            if (warehouse.IsOpen)
                await warehouse.Close();
        }
    }

    [Fact]
    public async Task PostgreSql_BuildsModelAndTranslatesQuery()
    {
        await AssertProviderCompatibility(
            options => options.UseNpgsql(
                "Host=localhost;Database=esiur_tests;Username=esiur;Password=unused"),
            "Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public async Task MySql_BuildsModelAndTranslatesQuery()
    {
        await AssertProviderCompatibility(
            options => options.UseMySQL(
                "Server=localhost;Database=esiur_tests;User=esiur;Password=unused"),
            "MySql.EntityFrameworkCore");
    }

    private static async Task AssertProviderCompatibility(
        Action<DbContextOptionsBuilder<ResourceDbContext>> configureProvider,
        string expectedProvider)
    {
        var warehouse = new Warehouse();
        var store = await warehouse.Put("database", new EntityStore());
        var builder = new DbContextOptionsBuilder<ResourceDbContext>();

        configureProvider(builder);
        builder.UseEsiur(store);

        await using var context = new ResourceDbContext(builder.Options);
        var entity = context.Model.FindEntityType(typeof(ProviderResource));
        var sql = context.Resources
            .Where(x => x.Name == "provider check")
            .ToQueryString();

        Assert.NotNull(entity);
        Assert.Equal(expectedProvider, context.Database.ProviderName);
        Assert.Contains(entity!.GetTableName()!, sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ResourceDbContext(DbContextOptions<ResourceDbContext> options)
        : DbContext(options)
    {
        public DbSet<ProviderResource> Resources => Set<ProviderResource>();
    }

}

public class ProviderResource : IResource
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Instance? Instance { get; set; }

    public event DestroyedEvent? OnDestroy;

    public AsyncReply<bool> Handle(
        ResourceOperation operation,
        IResourceContext? context = null)
        => new(true);

    public void Destroy() => OnDestroy?.Invoke(this);
}
