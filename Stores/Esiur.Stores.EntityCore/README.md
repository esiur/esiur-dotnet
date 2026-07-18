# Esiur Entity Framework Core Store

`Esiur.Stores.EntityCore` integrates Esiur resource materialization and paths
with Entity Framework Core 10. Version 3.0 targets .NET 10.

## Installation

```shell
dotnet add package Esiur.Stores.EntityCore --version 3.0.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.10
```

Install the package for the database provider used by your application before
configuring Esiur.

## Usage

Configure the database provider first, then attach an `EntityStore` using
`UseEsiur`:

```csharp
using Esiur.Resource;
using Esiur.Stores.EntityCore;
using Microsoft.EntityFrameworkCore;

var warehouse = new Warehouse();
var store = await warehouse.Put("database", new EntityStore());

DbContextOptions<AppDbContext>? options = null;
options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=app.db")
    .UseEsiur(store, () => new AppDbContext(options!))
    .Options;

await warehouse.Open();

await using var db = new AppDbContext(options);
await db.Database.EnsureCreatedAsync();
var device = await db.Devices.AddResourceAsync(
    new Device { Name = "sensor-1" });
```

An entity exposed as an Esiur resource can use the source generator:

```csharp
using System.ComponentModel.DataAnnotations;
using Esiur.Resource;
using Microsoft.EntityFrameworkCore;

[Resource]
public partial class Device
{
    [Key, Export]
    int id;

    [Export]
    string name = string.Empty;
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
}
```

The `UseEsiur` extension works with any EF Core provider. The Esiur test suite
covers SQLite persistence and materialization, plus PostgreSQL and MySQL model
creation and SQL translation.
