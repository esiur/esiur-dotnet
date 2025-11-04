using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace Esiur.Data;

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;

public enum NaNInfinityPolicy
{
    Throw,          // Default: throw on NaN/∞ when converting to non-floating types
    NullIfNullable, // If target is Nullable<decimal>, return null; otherwise throw
    CoerceZero      // Replace NaN/∞ with 0
}

public sealed class RuntimeCastOptions
{
    public bool CheckedNumeric { get; set; } = true;

    // For DateTime/DateTimeOffset parsing
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
    public DateTimeStyles DateTimeStyles { get; set; } = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal;

    // For enums
    public bool EnumIgnoreCase { get; set; } = true;
    public bool EnumMustBeDefined { get; set; } = false;

    // float/double → decimal behavior
    public NaNInfinityPolicy NaNInfinityPolicy { get; set; } = NaNInfinityPolicy.Throw;
}

public static class RuntimeCaster
{

    public static readonly RuntimeCastOptions Default = new RuntimeCastOptions();

    // (fromType, toType) -> converter(value, options)  (options captured at call time)
    private static readonly ConcurrentDictionary<(Type from, Type to), Func<object, RuntimeCastOptions, object>> _cache = new();

    // Numeric-only compiled converters (fast path), keyed by checked/unchecked
    private static readonly ConcurrentDictionary<(Type from, Type to, bool @checked), Func<object, object>> _numericCache = new();

    public static object Cast(object value, Type toType, RuntimeCastOptions? options = null)
    {
        options ??= Default;

        if (toType is null) throw new ArgumentNullException(nameof(toType));
        if (value is null)
        {
            if (IsNonNullableValueType(toType))
                throw new InvalidCastException($"Cannot cast null to non-nullable {toType}.");
            return null;
        }

        var fromType = value.GetType();
        if (toType.IsAssignableFrom(fromType)) return value; // already compatible

        var fn = _cache.GetOrAdd((fromType, toType), k => BuildConverter(k.from, k.to));
        return fn(value, options);
    }

    // ------------------------ Builder ------------------------
    private static Func<object, RuntimeCastOptions, object> BuildConverter(Type fromType, Type toType)
    {
        // Nullable handling is done inside ConvertCore.
        return (value, opts) => ConvertCore(value, fromType, toType, opts);
    }

    // ------------------------ Core Routing ------------------------
    private static object ConvertCore(object value, Type fromType, Type toType, RuntimeCastOptions opts)
    {
        var toUnderlying = Nullable.GetUnderlyingType(toType) ?? toType;
        var fromUnderlying = Nullable.GetUnderlyingType(fromType) ?? fromType;

        // If converting nullable source and it's null → result is null if target nullable, else throw
        if (!fromType.IsValueType && value is null)
        {
            if (IsNonNullableValueType(toType))
                throw new InvalidCastException($"Cannot cast null to non-nullable {toType}.");
            return null;
        }

        // Special cases first
        // 1) Enum targets
        if (toUnderlying.IsEnum)
            return ConvertToEnum(value, fromUnderlying, toType, toUnderlying, opts);

        // 2) Guid targets
        if (toUnderlying == typeof(Guid))
            return ConvertToGuid(value, fromUnderlying, toType);

        // 3) DateTime / DateTimeOffset targets
        if (toUnderlying == typeof(DateTime))
            return ConvertToDateTime(value, fromUnderlying, toType, opts);
        if (toUnderlying == typeof(DateTimeOffset))
            return ConvertToDateTimeOffset(value, fromUnderlying, toType, opts);

        // 4) decimal from float/double with NaN/∞ policy
        if (toUnderlying == typeof(decimal) && (fromUnderlying == typeof(float) || fromUnderlying == typeof(double)))
        {
            var dec = ConvertFloatDoubleToDecimal(value, fromUnderlying, opts, out bool useNull);
            if (toType != toUnderlying) // wrap in Nullable<decimal>
                return useNull ? null : (decimal?)dec;
            if (useNull) throw new OverflowException("NaN/Infinity cannot be converted to decimal.");
            return dec;
        }

        // 5) General numeric conversions via compiled expression
        if (IsNumeric(fromUnderlying) && IsNumeric(toUnderlying))
        {
            var nc = _numericCache.GetOrAdd((fromUnderlying, toUnderlying, opts.CheckedNumeric),
                k => BuildNumericConverter(k.from, k.to, k.@checked));
            var result = nc(value);
            // Wrap into nullable if needed
            if (toType != toUnderlying) return BoxNullable(result, toUnderlying);
            return result;
        }

        // 6) String <-> other basics (Use TypeConverter first; if no path, fall through)
        if (toUnderlying == typeof(string))
            return value?.ToString();

        // 7) Last-resort: TypeConverter or ChangeType once, inside this compiled path
        // Try TypeConverter(target)
        var tc = System.ComponentModel.TypeDescriptor.GetConverter(toUnderlying);
        if (tc.CanConvertFrom(fromUnderlying))
        {
            var r = tc.ConvertFrom(null, opts.Culture, value);
            if (toType != toUnderlying) return BoxNullable(r, toUnderlying);
            return r!;
        }

        // Try TypeConverter(source)
        var tc2 = System.ComponentModel.TypeDescriptor.GetConverter(fromUnderlying);
        if (tc2.CanConvertTo(toUnderlying))
        {
            var r = tc2.ConvertTo(null, opts.Culture, value, toUnderlying);
            if (toType != toUnderlying) return BoxNullable(r, toUnderlying);
            return r!;
        }

        // Try Convert.ChangeType for IConvertible fallbacks
        try
        {
            var r = Convert.ChangeType(value, toUnderlying, opts.Culture);
            if (toType != toUnderlying) return BoxNullable(r, toUnderlying);
            return r!;
        }
        catch
        {
            // Final attempt: assignable cast via reflection (e.g., interfaces)
            if (toUnderlying.IsInstanceOfType(value))
            {
                if (toType != toUnderlying) return BoxNullable(value, toUnderlying);
                return value;
            }
            throw new InvalidCastException($"Cannot cast {fromType} to {toType}.");
        }
    }

    // ------------------------ Helpers: Numeric ------------------------
    private static Func<object, object> BuildNumericConverter(Type from, Type to, bool @checked)
    {
        var p = Expression.Parameter(typeof(object), "v");

        Expression val = from.IsValueType
            ? Expression.Unbox(p, from)
            : Expression.Convert(p, from);

        Expression body;
        try
        {
            body = @checked ? Expression.ConvertChecked(val, to) : Expression.Convert(val, to);
        }
        catch (InvalidOperationException)
        {
            // Non-legal numeric convert — should be rare given IsNumeric check.
            throw new InvalidCastException($"Numeric conversion not supported: {from} -> {to}");
        }

        // Box result to object
        Expression boxed = to.IsValueType ? Expression.Convert(body, typeof(object)) : body;
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
        => t.IsValueType && Nullable.GetUnderlyingType(t) is null;

    private static object BoxNullable(object value, Type underlying)
    {
        // Create Nullable<T>(value) then box
        var nt = typeof(Nullable<>).MakeGenericType(underlying);
        var ctor = nt.GetConstructor(new[] { underlying })!;
        return ctor.Invoke(new[] { value });
    }

    // ------------------------ Helpers: NaN/∞ to decimal ------------------------
    private static decimal ConvertFloatDoubleToDecimal(object value, Type fromUnderlying, RuntimeCastOptions opts, out bool useNull)
    {
        useNull = false;

        if (fromUnderlying == typeof(float))
        {
            var f = (float)value;
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                switch (opts.NaNInfinityPolicy)
                {
                    case NaNInfinityPolicy.NullIfNullable: useNull = true; return default;
                    case NaNInfinityPolicy.CoerceZero: return 0m;
                    default: throw new OverflowException("Cannot convert NaN/Infinity to decimal.");
                }
            }
            return opts.CheckedNumeric ? checked((decimal)f) : (decimal)f;
        }
        else // double
        {
            var d = (double)value;
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                switch (opts.NaNInfinityPolicy)
                {
                    case NaNInfinityPolicy.NullIfNullable: useNull = true; return default;
                    case NaNInfinityPolicy.CoerceZero: return 0m;
                    default: throw new OverflowException("Cannot convert NaN/Infinity to decimal.");
                }
            }
            return opts.CheckedNumeric ? checked((decimal)d) : (decimal)d;
        }
    }

    // ------------------------ Helpers: Enum ------------------------
    private static object ConvertToEnum(object value, Type fromUnderlying, Type toType, Type enumType, RuntimeCastOptions opts)
    {
        // Nullable<Enum> wrapping
        bool wrapNullable = toType != enumType;

        // String → Enum
        if (fromUnderlying == typeof(string))
        {
            var parsed = Enum.Parse(enumType, (string)value, opts.EnumIgnoreCase);

            if (opts.EnumMustBeDefined && !Enum.IsDefined(enumType, parsed!))
                throw new InvalidCastException($"Value '{value}' is not a defined member of {enumType.Name}.");

            return wrapNullable ? BoxNullable(parsed!, enumType) : parsed!;
        }

        // Numeric → Enum
        if (IsNumeric(fromUnderlying))
        {
            // Convert numeric to enum’s underlying integral type first (checked/unchecked handled by compiled numeric path)
            var et = Enum.GetUnderlyingType(enumType);
            var numConv = _numericCache.GetOrAdd((fromUnderlying, et, true), k => BuildNumericConverter(k.from, k.to, k.@checked));
            var integral = numConv(value);

            var enumObj = Enum.ToObject(enumType, integral);
            if (opts.EnumMustBeDefined && !Enum.IsDefined(enumType, enumObj))
                throw new InvalidCastException($"Numeric value {integral} is not a defined member of {enumType.Name}.");

            return wrapNullable ? BoxNullable(enumObj, enumType) : enumObj;
        }

        // Fallback: not supported
        throw new InvalidCastException($"Cannot cast {fromUnderlying} to enum {enumType.Name}.");
    }

    // ------------------------ Helpers: Guid ------------------------
    private static object ConvertToGuid(object value, Type fromUnderlying, Type toType)
    {
        bool wrapNullable = toType != typeof(Guid);

        if (fromUnderlying == typeof(string))
        {
            if (!Guid.TryParse((string)value, out var g))
                throw new InvalidCastException($"Cannot parse '{value}' to Guid.");
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

        throw new InvalidCastException($"Cannot cast {fromUnderlying} to Guid.");
    }

    // ------------------------ Helpers: DateTime / DateTimeOffset ------------------------
    private static object ConvertToDateTime(object value, Type fromUnderlying, Type toType, RuntimeCastOptions opts)
    {
        bool wrapNullable = toType != typeof(DateTime);

        if (fromUnderlying == typeof(string))
        {
            if (!DateTime.TryParse((string)value, opts.Culture, opts.DateTimeStyles, out var dt))
                throw new InvalidCastException($"Cannot parse '{value}' to DateTime.");
            return wrapNullable ? (DateTime?)dt : dt;
        }

        if (fromUnderlying == typeof(long))
        {
            // Treat as ticks
            var dt = new DateTime((long)value, DateTimeKind.Unspecified);
            return wrapNullable ? (DateTime?)dt : dt;
        }

        if (fromUnderlying == typeof(double))
        {
            // Treat as OADate if finite
            var d = (double)value;
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new InvalidCastException("Cannot convert NaN/Infinity to DateTime.");
            var dt = DateTime.FromOADate(d);
            return wrapNullable ? (DateTime?)dt : dt;
        }

        throw new InvalidCastException($"Cannot cast {fromUnderlying} to DateTime.");
    }

    private static object ConvertToDateTimeOffset(object value, Type fromUnderlying, Type toType, RuntimeCastOptions opts)
    {
        bool wrapNullable = toType != typeof(DateTimeOffset);

        if (fromUnderlying == typeof(string))
        {
            if (!DateTimeOffset.TryParse((string)value, opts.Culture, opts.DateTimeStyles, out var dto))
                throw new InvalidCastException($"Cannot parse '{value}' to DateTimeOffset.");
            return wrapNullable ? (DateTimeOffset?)dto : dto;
        }

        if (fromUnderlying == typeof(long))
        {
            // Treat as ticks since 0001-01-01
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

        throw new InvalidCastException($"Cannot cast {fromUnderlying} to DateTimeOffset.");
    }
}
