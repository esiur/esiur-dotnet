using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Esiur.Data
{
    internal static class IndexedStructureCodec
    {
        private sealed class IndexedMember
        {
            public byte Index { get; }
            public string Name { get; }
            public Type Type { get; }
            public Func<object, object?> Get { get; }
            public Action<object, object?> Set { get; }

            public IndexedMember(byte index, PropertyInfo property)
            {
                Index = index;
                Name = property.Name;
                Type = property.PropertyType;
                Get = property.GetValue;
                Set = property.SetValue;
            }

            public IndexedMember(byte index, FieldInfo field)
            {
                Index = index;
                Name = field.Name;
                Type = field.FieldType;
                Get = field.GetValue;
                Set = field.SetValue;
            }
        }

        private static readonly ConcurrentDictionary<Type, IndexedMember[]> Members = new();

        internal static Map<byte, object?> ToMap(IndexedStructure value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            var map = new Map<byte, object?>();
            foreach (var member in GetMembers(value.GetType()))
            {
                var memberValue = member.Get(value);
                if (memberValue is not null)
                    map[member.Index] = PrepareForComposition(memberValue);
            }

            return map;
        }

        internal static T FromMap<T>(object value) where T : IndexedStructure
            => (T)FromMap(value, typeof(T));

        internal static object FromMap(object value, Type targetType)
        {
            if (!typeof(IndexedStructure).IsAssignableFrom(targetType))
                throw new ArgumentException($"{targetType.FullName} does not inherit {nameof(IndexedStructure)}.",
                                            nameof(targetType));

            if (value is not IDictionary map)
                throw new InvalidDataException(
                    $"Cannot parse {targetType.FullName}: expected an indexed map but received {value?.GetType().FullName ?? "null"}.");

            object instance;
            try
            {
                instance = Activator.CreateInstance(targetType, true)!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Structure type {targetType.FullName} must have a parameterless constructor.", ex);
            }

            var members = GetMembers(targetType).ToDictionary(x => x.Index);
            foreach (DictionaryEntry entry in map)
            {
                byte index;
                try
                {
                    index = Convert.ToByte(entry.Key);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("A structure field index is not a byte value.", ex);
                }

                // Sparse structures are version tolerant: fields unknown to this CLR type are ignored.
                if (!members.TryGetValue(index, out var member))
                    continue;

                try
                {
                    member.Set(instance, ConvertValue(entry.Value, member.Type));
                }
                catch (Exception ex) when (ex is not InvalidDataException)
                {
                    throw new InvalidDataException(
                        $"Could not assign structure field {targetType.Name}.{member.Name} (index {index}).", ex);
                }
            }

            return instance;
        }

        private static IndexedMember[] GetMembers(Type type)
            => Members.GetOrAdd(type, DiscoverMembers);

        private static IndexedMember[] DiscoverMembers(Type type)
        {
            if (!typeof(IndexedStructure).IsAssignableFrom(type))
                throw new InvalidOperationException($"{type.FullName} does not inherit {nameof(IndexedStructure)}.");

            var members = new List<IndexedMember>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (var property in type.GetProperties(flags))
            {
                var attribute = property.GetCustomAttribute<IndexAttribute>(true);
                if (attribute is null)
                    continue;

                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException(
                        $"Indexed property {type.FullName}.{property.Name} must be a readable, writable, non-indexer property.");

                members.Add(new IndexedMember(attribute.Index, property));
            }

            foreach (var field in type.GetFields(flags))
            {
                var attribute = field.GetCustomAttribute<IndexAttribute>(true);
                if (attribute is null)
                    continue;

                if (field.IsInitOnly || field.IsLiteral)
                    throw new InvalidOperationException(
                        $"Indexed field {type.FullName}.{field.Name} must be writable.");

                members.Add(new IndexedMember(attribute.Index, field));
            }

            var duplicate = members.GroupBy(x => x.Index).FirstOrDefault(x => x.Count() > 1);
            if (duplicate is not null)
                throw new InvalidOperationException(
                    $"Structure type {type.FullName} uses index {duplicate.Key} more than once.");

            return members.OrderBy(x => x.Index).ToArray();
        }

        private static object? PrepareForComposition(object? value)
        {
            if (value is IndexedStructure structure)
                return ToMap(structure);

            // Protocol-owned enums are values, not distributed enum TypeDefs. Carry their
            // underlying integer so an indexed structure has no warehouse-registration dependency.
            if (value is Enum enumValue)
                return Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()));

            // A collection containing structures cannot advertise the local CLR structure type
            // through TRU. Encode it as a dynamic list and recursively turn its items into maps.
            if (value is IEnumerable sequence && value is not string && value is not byte[] && value is not IDictionary)
            {
                var items = sequence.Cast<object?>().ToArray();
                if (items.Any(x => x is IndexedStructure || ContainsStructure(x?.GetType())))
                    return items.Select(PrepareForComposition).ToArray();
            }

            return value;
        }

        internal static bool ContainsStructure(Type? type)
        {
            if (type is null)
                return false;
            if (typeof(IndexedStructure).IsAssignableFrom(type))
                return true;
            if (type.IsArray)
                return ContainsStructure(type.GetElementType());
            if (type.IsGenericType)
                return type.GetGenericArguments().Any(ContainsStructure);
            return false;
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value is null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            if (targetType.IsInstanceOfType(value))
                return value;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (typeof(IndexedStructure).IsAssignableFrom(underlyingType))
                return FromMap(value, underlyingType);

            if (targetType.IsArray && value is IEnumerable arraySource)
            {
                var elementType = targetType.GetElementType()!;
                var values = arraySource.Cast<object?>().Select(x => ConvertValue(x, elementType)).ToArray();
                var array = Array.CreateInstance(elementType, values.Length);
                for (var i = 0; i < values.Length; i++)
                    array.SetValue(values[i], i);
                return array;
            }

            if (TryGetListElementType(targetType, out var listElementType) && value is IEnumerable listSource)
            {
                var concreteType = targetType.IsInterface || targetType.IsAbstract
                    ? typeof(List<>).MakeGenericType(listElementType)
                    : targetType;
                var list = (IList)Activator.CreateInstance(concreteType)!;
                foreach (var item in listSource)
                    list.Add(ConvertValue(item, listElementType));
                return list;
            }

            if (TryGetMapTypes(targetType, out var keyType, out var valueType) && value is IDictionary sourceMap)
            {
                var concreteType = targetType.IsInterface || targetType.IsAbstract
                    ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
                    : targetType;
                var targetMap = (IDictionary)Activator.CreateInstance(concreteType)!;
                foreach (DictionaryEntry item in sourceMap)
                    targetMap.Add(ConvertValue(item.Key, keyType), ConvertValue(item.Value, valueType));
                return targetMap;
            }

            return RuntimeCaster.Cast(value, targetType);
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            var candidate = type.IsGenericType &&
                            (type.GetGenericTypeDefinition() == typeof(List<>) ||
                             type.GetGenericTypeDefinition() == typeof(IList<>) ||
                             type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                             type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ? type
                : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType &&
                    x.GetGenericTypeDefinition() == typeof(IList<>));

            elementType = candidate?.GetGenericArguments()[0] ?? typeof(object);
            return candidate is not null;
        }

        private static bool TryGetMapTypes(Type type, out Type keyType, out Type valueType)
        {
            var candidate = type.IsGenericType &&
                            (type.GetGenericTypeDefinition() == typeof(Map<,>) ||
                             type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                             type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                ? type
                : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType &&
                    x.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            var arguments = candidate?.GetGenericArguments();
            keyType = arguments?[0] ?? typeof(object);
            valueType = arguments?[1] ?? typeof(object);
            return candidate is not null;
        }
    }

}
