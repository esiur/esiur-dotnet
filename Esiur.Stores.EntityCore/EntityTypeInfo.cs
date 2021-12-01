using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Esiur.Stores.EntityCore;

struct EntityTypeInfo
{
    public string Name;
    public IEntityType Type;
    public PropertyInfo PrimaryKey;
    // public Func<DbContext> Getter;
}
