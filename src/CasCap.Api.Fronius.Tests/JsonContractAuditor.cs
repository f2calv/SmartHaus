using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasCap.Tests;

/// <summary>
/// Compares a JSON payload with its DTO graph and reports properties that would be discarded.
/// </summary>
public static class JsonContractAuditor
{
    /// <summary>
    /// Finds JSON properties that have no corresponding property in the supplied DTO type.
    /// </summary>
    public static IReadOnlyList<string> FindUnmappedProperties<T>(string json)
        => FindUnmappedProperties(json, typeof(T));

    /// <summary>
    /// Finds JSON properties that have no corresponding property in the supplied DTO type.
    /// </summary>
    public static IReadOnlyList<string> FindUnmappedProperties(string json, Type targetType)
    {
        using var document = JsonDocument.Parse(json);
        var unmappedProperties = new List<string>();
        Inspect(document.RootElement, targetType, "$", unmappedProperties);
        return unmappedProperties;
    }

    private static void Inspect(JsonElement element, Type targetType, string path, List<string> unmappedProperties)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (targetType == typeof(object) || targetType == typeof(JsonElement))
            return;

        if (TryGetDictionaryValueType(targetType, out var valueType))
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                    Inspect(property.Value, valueType, $"{path}.{property.Name}", unmappedProperties);
            }
            return;
        }

        if (TryGetEnumerableElementType(targetType, out var elementType))
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Inspect(item, elementType, $"{path}[{index++}]", unmappedProperties);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object || IsScalar(targetType))
            return;

        var properties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .ToDictionary(
                property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var jsonProperty in element.EnumerateObject())
        {
            if (!properties.TryGetValue(jsonProperty.Name, out var dtoProperty))
            {
                unmappedProperties.Add($"{path}.{jsonProperty.Name}");
                continue;
            }

            Inspect(jsonProperty.Value, dtoProperty.PropertyType, $"{path}.{jsonProperty.Name}", unmappedProperties);
        }
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionaryType = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        valueType = dictionaryType?.GetGenericArguments()[1] ?? typeof(object);
        return dictionaryType is not null;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }

        elementType = type.IsArray
            ? type.GetElementType()!
            : type.GetInterfaces()
                .Append(type)
                .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0] ?? typeof(object);

        return type.IsArray || elementType != typeof(object);
    }

    private static bool IsScalar(Type type)
        => type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(Guid);
}