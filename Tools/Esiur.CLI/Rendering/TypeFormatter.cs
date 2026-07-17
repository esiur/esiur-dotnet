using Esiur.Data;

namespace Esiur.CLI.Rendering;

public static class TypeFormatter
{
    public static string Format(Tru? type)
    {
        if (type is null) return "Dynamic";
        if (type is TruTypeDef reference && reference.TypeDef is not null)
            return reference.TypeDef.Name + (type.Nullable ? "?" : string.Empty);
        return type.ToString() ?? type.Identifier.ToString();
    }
}

public static class TypeIdFormatter
{
    public static string Format(ulong id) => $"0x{id:x16}";
}
