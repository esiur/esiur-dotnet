#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Esiur.Data;

// --- Policies & Options (NET Standard 2.0 compatible) --------------------

public enum NaNInfinityPolicy
{
    Throw,
    NullIfNullable,
    CoerceZero
}

public sealed class RuntimeCastOptions
{
    // Reusable default to avoid per-call allocations
    public static readonly RuntimeCastOptions Default = new RuntimeCastOptions();

    public bool CheckedNumeric { get; set; } = true;

    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    public DateTimeStyles DateTimeStyles { get; set; } =
        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal;

    public bool EnumIgnoreCase { get; set; } = true;
    public bool EnumMustBeDefined { get; set; } = false;

    public NaNInfinityPolicy NaNInfinityPolicy { get; set; } = NaNInfinityPolicy.Throw;
}

// --- Core Caster ----------------------------------------------------------

public static class RuntimeCaster
{
    // (fromType, toType) -> converter(value, options)
    private static readonly ConcurrentDictionary<(Type from, Type to),
        Func<object?, RuntimeCastOptions, object?>> _cache =
        new ConcurrentDictionary<(Type, Type), Func<object?, RuntimeCastOptions, object?>>();

    // Numeric-only compiled converters
    private static readonly ConcurrentDictionary<(Type from, Type to, bool @checked),
        Func<object, object>> _numericCache =
        new ConcurrentDictionary<(Type, Type, bool), Func<object, object>>();

    // Per-element converters for collections
    private static readonly ConcurrentDictionary<(Type from, Type to),
        Func<object?, RuntimeCastOptions, object?>> _elemConvCache =
        new ConcurrentDictionary<(Type, Type), Func<object?, RuntimeCastOptions, object?>>();

    // --------- Zero-allocation convenience overloads ---------
    public static object? Cast(object? value, Type toType)
        => Cast(value, toType, RuntimeCastOptions.Default);

    public static object? CastSequence(object? value, Type toType)
        => CastSequence(value, toType, RuntimeCastOptions.Default);

    // --------- Main API (options accepted if you want different policies) ---------
    public static object? Cast(object? value, Type toType, RuntimeCastOptions? options)
    {
        if (toType == null) throw new ArgumentNullException(nameof(toType));
        var opts = options ?? RuntimeCastOptions.Default;

        if (value == null)
        {
            if (IsNonNullableValueType(toType))
                throw new InvalidCastException("Cannot cast null to non-nullable " + toType + ".");
            return null;
        }

        var fromType = value.GetType();
        if (toType.IsAssignableFrom(fromType)) return value;

        var fn = _cache.GetOrAdd((fromType, toType), k => BuildConverter(k.from, k.to));
        return fn(value, opts);
    }

    public static object? CastSequence(object? value, Type toType, RuntimeCastOptions? options)
    {
        var opts = options ?? RuntimeCastOptions.Default;
        if (value == null) return null;

        var fromType = value.GetType();
        var toUnderlying = Nullable.GetUnderlyingType(toType) ?? toType;

        if (!IsSupportedSeqType(fromType) || !IsSupportedSeqType(toUnderlying))
            throw new InvalidCastException("Only 1D arrays and List<T> are supported. " + fromType + " → " + toType);

        var fromElem = GetElementType(fromType)!;
        var toElem = GetElementType(toUnderlying)!;

        // Fast path: same element type
        if (fromElem == toElem)
        {
            if (fromType.IsArray && IsListType(toUnderlying))
                return ArrayToListDirect((Array)value, toUnderlying);

            if (IsListType(fromType) && toUnderlying.IsArray)
                return ListToArrayDirect((IList)value, toElem);

            if (fromType.IsArray && toUnderlying.IsArray)
                return ArrayToArrayDirect((Array)value, toElem);

            if (IsListType(fromType) && IsListType(toUnderlying))
                return ListToListDirect((IList)value, toUnderlying, toElem);
        }

        // General path with per-element converter
        var elemConv = _elemConvCache.GetOrAdd((fromElem, toElem),
            k => (object? elem, RuntimeCastOptions o) =>
            {
                if (elem == null) return null;
                return Cast(elem, toElem, o);
            });

        if (fromType.IsArray && IsListType(toUnderlying))
            return ArrayToListConverted((Array)value, toUnderlying, toElem, elemConv, opts);

        if (IsListType(fromType) && toUnderlying.IsArray)
            return ListToArrayConverted((IList)value, toElem, elemConv, opts);

        if (fromType.IsArray && toUnderlying.IsArray)
            return ArrayToArrayConverted((Array)value, toElem, elemConv, opts);

        if (IsListType(fromType) && IsListType(toUnderlying))
            return ListToListConverted((IList)value, toUnderlying, toElem, elemConv, opts);

        throw new InvalidCastException("Unsupported sequence cast " + fromType + " → " + toType + ".");
    }

    // ------------------------ Builder ------------------------

    private static Func<object?, RuntimeCastOptions, object?> BuildConverter(Type fromType, Type toType)
    {
        return (value, opts) => ConvertCore(value!, fromType, toType, opts);
    }

    // ------------------------ Core Routing ------------------------

    private static object? ConvertCore(object value, Type fromType, Type toType, RuntimeCastOptions opts)
    {
        var toUnderlying = Nullable.GetUnderlyingType(toType) ?? toType;
        var fromUnderlying = Nullable.GetUnderlyingType(fromType) ?? fromType;

        // Collections early
        {
            bool handled;
            var coll = ConvertCollectionsIfAny(value, fromType, toType, opts, out handled);
            if (handled) return coll;
        }

        // Enum
        if (toUnderlying.IsEnum)
            return ConvertToEnum(value, fromUnderlying, toType, toUnderlying, opts);

        // Guid
        if (toUnderlying == typeof(Guid))
            return ConvertToGuid(value, fromUnderlying, toType);

        // Date/Time
        if (toUnderlying == typeof(DateTime))
            return ConvertToDateTime(value, fromUnderlying, toType, opts);

        if (toUnderlying == typeof(DateTimeOffset))
            return ConvertToDateTimeOffset(value, fromUnderlying, toType, opts);

        // float/double -> decimal with policy
        if (toUnderlying == typeof(decimal) &&
            (fromUnderlying == typeof(float) || fromUnderlying == typeof(double)))
        {
            bool useNull;
            var dec = ConvertFloatDoubleToDecimal(value, fromUnderlying, opts, out useNull);
            if (toType != toUnderlying) // Nullable<decimal>
                return useNull ? null : (decimal?)dec;
            if (useNull) throw new OverflowException("NaN/Infinity cannot be converted to decimal.");
            return dec;
        }

        // Numeric -> Numeric
        if (IsNumeric(fromUnderlying) && IsNumeric(toUnderlying))
        {
            var nc = _numericCache.GetOrAdd((fromUnderlying, toUnderlying, opts.CheckedNumeric),
                k => BuildNumericConverter(k.from, k.to, k.@checked));
            var result = nc(value);
            if (toType != toUnderlying) return BoxNullable(result, toUnderlying);
            return result;
        }

        // To string
        if (toUnderlying == typeof(string))
            return value != null ? value.ToString() : null;

        // TypeConverter(target)
        var tc = System.ComponentModel.TypeDescriptor.GetConverter(toUnderlying);
        if (tc.CanConvertFrom(fromUnderlying))
        {
            var r = tc.ConvertFrom(null, opts.Culture, value);
            if (toType != toUnderlying) return BoxNullable(r!, toUnderlying);
            return r!;
        }

        // TypeConverter(source)
        var tc2 = System.ComponentModel.TypeDescriptor.GetConverter(fromUnderlying);
        if (tc2.CanConvertTo(toUnderlying))
        {
            var r = tc2.ConvertTo(null, opts.Culture, value, toUnderlying);
            if (toType != toUnderlying) return BoxNullable(r!, toUnderlying);
            return r!;
        }

        // Convert.ChangeType fallback
        try
        {
            var r = Convert.ChangeType(value, toUnderlying, opts.Culture);
            if (toType != toUnderlying) return BoxNullable(r!, toUnderlying);
            return r!;
        }
        catch
        {
            if (toUnderlying.IsInstanceOfType(value))
            {
                if (toType != toUnderlying) return BoxNullable(value, toUnderlying);
                return value;
            }
            throw new InvalidCastException("Cannot cast " + fromType + " to " + toType + ".");
        }
    }

    private static object? ConvertCollectionsIfAny(object value, Type fromType, Type toType, RuntimeCastOptions opts, out bool handled)
    {
        handled = false;
        var toUnderlying = Nullable.GetUnderlyingType(toType) ?? toType;

        if (!IsSupportedSeqType(fromType) || !IsSupportedSeqType(toUnderlying))
            return value;

        handled = true;
        return CastSequence(value, toUnderlying, opts);
    }

    // ------------------------ Numeric Helpers ------------------------

    private static Func<object, object> BuildNumericConverter(Type from, Type to, bool @checked)
    {
        var p = Expression.Parameter(typeof(object), "v");
        Expression val = from.IsValueType ? (Expression)Expression.Unbox(p, from)
                                          : (Expression)Expression.Convert(p, from);

        Expression body;
        try
        {
            body = @checked ? (Expression)Expression.ConvertChecked(val, to)
                            : (Expression)Expression.Convert(val, to);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidCastException("Numeric conversion not supported: " + from + " -> " + to);
        }

        Expression boxed = to.IsValueType ? (Expression)Expression.Convert(body, typeof(object)) : body;
        return Expression.Lambda<Func<object, object>>(boxed, p).Compile();
    }

    private static bool IsNumeric(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) ||
               t == typeof(decimal);
    }

    private static bool IsNonNullableValueType(Type t)
    {
        return t.IsValueType && Nullable.GetUnderlyingType(t) == null;
    }

    private static object BoxNullable(object value, Type underlying)
    {
        var nt = typeof(Nullable<>).MakeGenericType(underlying);
        var ctor = nt.GetConstructor(new[] { underlying });
        return ctor!.Invoke(new[] { value });
    }

    // ------------------------ NaN/∞ to decimal ------------------------

    private static decimal ConvertFloatDoubleToDecimal(object value, Type fromUnderlying, RuntimeCastOptions opts, out bool useNull)
    {
        useNull = false;

        if (fromUnderlying == typeof(float))
        {
            var f = (float)value;
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                if (opts.NaNInfinityPolicy == NaNInfinityPolicy.NullIfNullable) { useNull = true; return 0m; }
                if (opts.NaNInfinityPolicy == NaNInfinityPolicy.CoerceZero) return 0m;
                throw new OverflowException("Cannot convert NaN/Infinity to decimal.");
            }
            return opts.CheckedNumeric ? checked((decimal)f) : (decimal)f;
        }
        else
        {
            var d = (double)value;
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                if (opts.NaNInfinityPolicy == NaNInfinityPolicy.NullIfNullable) { useNull = true; return 0m; }
                if (opts.NaNInfinityPolicy == NaNInfinityPolicy.CoerceZero) return 0m;
                throw new OverflowException("Cannot convert NaN/Infinity to decimal.");
            }
            return opts.CheckedNumeric ? checked((decimal)d) : (decimal)d;
        }
    }

    // ------------------------ Enum ------------------------
    // Note: .NET Standard 2.0 lacks non-generic TryParse(Type, …, ignoreCase).
    // We use Enum.Parse(Type, string, bool ignoreCase) with try/catch.

    private static object? ConvertToEnum(object value, Type fromUnderlying, Type toType, Type enumType, RuntimeCastOptions opts)
    {
        bool wrapNullable = toType != enumType;

        if (fromUnderlying == typeof(string))
        {
            object parsed;
            try
            {
                parsed = Enum.Parse(enumType, (string)value, opts.EnumIgnoreCase);
            }
            catch (ArgumentException)
            {
                throw new InvalidCastException("Cannot parse '" + value + "' to " + enumType.Name + ".");
            }

            if (opts.EnumMustBeDefined && !Enum.IsDefined(enumType, parsed))
                throw new InvalidCastException("Value '" + value + "' is not a defined member of " + enumType.Name + ".");

            return wrapNullable ? BoxNullable(parsed, enumType) : parsed;
        }

        if (IsNumeric(fromUnderlying))
        {
            var et = Enum.GetUnderlyingType(enumType);
            var numConv = _numericCache.GetOrAdd((fromUnderlying, et, true),
                k => BuildNumericConverter(k.from, k.to, k.@checked));
            var integral = numConv(value);

            var enumObj = Enum.ToObject(enumType, integral);
            if (opts.EnumMustBeDefined && !Enum.IsDefined(enumType, enumObj))
                throw new InvalidCastException("Numeric value " + integral + " is not a defined member of " + enumType.Name + ".");

            return wrapNullable ? BoxNullable(enumObj, enumType) : enumObj;
        }

        throw new InvalidCastException("Cannot cast " + fromUnderlying + " to enum " + enumType.Name + ".");
    }

    // ------------------------ Guid ------------------------

    private static object? ConvertToGuid(object value, Type fromUnderlying, Type toType)
    {
        bool wrapNullable = toType != typeof(Guid);

        if (fromUnderlying == typeof(string))
        {
            Guid g;
            if (!Guid.TryParse((string)value, out g))
                throw new InvalidCastException("Cannot parse '" + value + "' to Guid.");
            return wrapNullable ? (Guid?)g : g;
        }

        if (fromUnderlying == typeof(byte[]))
        {
            var bytes = (byte[])value;
            if (bytes.Length != 16)
                throw new InvalidCastException("Guid requires a 16-byte array.");
            var g = new Guid(bytes);
            return wrapNullable ? (Guid?)g : g;
        }

        throw new InvalidCastException("Cannot cast " + fromUnderlying + " to Guid.");
    }

    // ------------------------ DateTime / DateTimeOffset ------------------------

    private static object? ConvertToDateTime(object value, Type fromUnderlying, Type toType, RuntimeCastOptions opts)
    {
        bool wrapNullable = toType != typeof(DateTime);

        if (fromUnderlying == typeof(string))
        {
            DateTime dt;
            if (!DateTime.TryParse((string)value, opts.Culture, opts.DateTimeStyles, out dt))
                throw new InvalidCastException("Cannot parse '" + value + "' to DateTime.");
            return wrapNullable ? (DateTime?)dt : dt;
        }

        if (fromUnderlying == typeof(long))
        {
            var dt = new DateTime((long)value, DateTimeKind.Unspecified);
            return wrapNullable ? (DateTime?)dt : dt;
        }

        if (fromUnderlying == typeof(double))
        {
            var d = (double)value;
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new InvalidCastException("Cannot convert NaN/Infinity to DateTime.");
            var dt = DateTime.FromOADate(d);
            return wrapNullable ? (DateTime?)dt : dt;
        }

        throw new InvalidCastException("Cannot cast " + fromUnderlying + " to DateTime.");
    }

    private static object? ConvertToDateTimeOffset(object value, Type fromUnderlying, Type toType, RuntimeCastOptions opts)
    {
        bool wrapNullable = toType != typeof(DateTimeOffset);

        if (fromUnderlying == typeof(string))
        {
            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse((string)value, opts.Culture, opts.DateTimeStyles, out dto))
                throw new InvalidCastException("Cannot parse '" + value + "' to DateTimeOffset.");
            return wrapNullable ? (DateTimeOffset?)dto : dto;
        }

        if (fromUnderlying == typeof(long))
        {
            var dto = new DateTimeOffset(new DateTime((long)value, DateTimeKind.Unspecified));
            return wrapNullable ? (DateTimeOffset?)dto : dto;
        }

        if (fromUnderlying == typeof(double))
        {
            var d = (double)value;
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new InvalidCastException("Cannot convert NaN/Infinity to DateTimeOffset.");
            var dt = DateTime.FromOADate(d);
            var dto = new DateTimeOffset(dt);
            return wrapNullable ? (DateTimeOffset?)dto : dto;
        }

        throw new InvalidCastException("Cannot cast " + fromUnderlying + " to DateTimeOffset.");
    }

    // ------------------------ Collections (arrays/lists) ------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsListType(Type t)
    {
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSupportedSeqType(Type t)
    {
        return (t.IsArray && t.GetArrayRank() == 1) || IsListType(t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Type? GetElementType(Type t)
    {
        if (t.IsArray) return t.GetElementType();
        if (IsListType(t)) return t.GetGenericArguments()[0];
        return null;
    }

    // Fast-path (same element types)
    private static object ArrayToListDirect(Array src, Type listTarget)
    {
        var list = (IList)Activator.CreateInstance(listTarget, src.Length)!;
        foreach (var e in src) list.Add(e);
        return list;
    }

    private static object ListToArrayDirect(IList src, Type elemType)
    {
        var arr = Array.CreateInstance(elemType, src.Count);
        for (int i = 0; i < src.Count; i++) arr.SetValue(src[i], i);
        return arr;
    }

    private static object ArrayToArrayDirect(Array src, Type elemType)
    {
        var dst = Array.CreateInstance(elemType, src.Length);
        Array.Copy(src, dst, src.Length);
        return dst;
    }

    private static object ListToListDirect(IList src, Type listTarget, Type elemType)
    {
        var list = (IList)Activator.CreateInstance(listTarget, src.Count)!;
        for (int i = 0; i < src.Count; i++) list.Add(src[i]);
        return list;
    }

    // Converted element paths
    private static object ArrayToListConverted(
        Array src, Type listTarget, Type toElem,
        Func<object?, RuntimeCastOptions, object?> elemConv, RuntimeCastOptions opts)
    {
        var list = (IList)Activator.CreateInstance(listTarget, src.Length)!;
        for (int i = 0; i < src.Length; i++)
        {
            var v = src.GetValue(i);
            list.Add(elemConv(v, opts));
        }
        return list;
    }

    private static object ListToArrayConverted(
        IList src, Type toElem,
        Func<object?, RuntimeCastOptions, object?> elemConv, RuntimeCastOptions opts)
    {
        var arr = Array.CreateInstance(toElem, src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var v = src[i];
            arr.SetValue(elemConv(v, opts), i);
        }
        return arr;
    }

    private static object ArrayToArrayConverted(
        Array src, Type toElem,
        Func<object?, RuntimeCastOptions, object?> elemConv, RuntimeCastOptions opts)
    {
        var dst = Array.CreateInstance(toElem, src.Length);
        for (int i = 0; i < src.Length; i++)
        {
            var v = src.GetValue(i);
            dst.SetValue(elemConv(v, opts), i);
        }
        return dst;
    }

    private static object ListToListConverted(
        IList src, Type listTarget, Type toElem,
        Func<object?, RuntimeCastOptions, object?> elemConv, RuntimeCastOptions opts)
    {
        var dst = (IList)Activator.CreateInstance(listTarget, src.Count)!;
        for (int i = 0; i < src.Count; i++)
        {
            var v = src[i];
            dst.Add(elemConv(v, opts));
        }
        return dst;
    }
}

