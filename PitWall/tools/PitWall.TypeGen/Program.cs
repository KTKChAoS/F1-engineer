using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitWall.Contracts.Messages;

var outputPath = args.Length == 0 ? Path.Combine("src", "contracts.generated.ts") : args[0];
var assembly = typeof(LivePayload).Assembly;
var nullability = new NullabilityInfoContext();
var types = assembly.GetTypes();
var enums = types.Where(type => type.IsEnum && type.Namespace == "PitWall.Contracts.Enums")
    .OrderBy(type => type.Name)
    .ToArray();
var messages = types.Where(type => type.IsClass && type.IsPublic && type.Namespace == "PitWall.Contracts.Messages" &&
                                   !(type.IsAbstract && type.IsSealed) &&
                                   !(type.Name == "WireMessage" && !type.IsGenericTypeDefinition))
    .OrderBy(type => type.Name)
    .ToArray();

var generated = new StringBuilder();
generated.AppendLine("// Generated from PitWall.Contracts. Do not edit by hand.");
generated.AppendLine();

foreach (var enumType in enums)
{
    var values = Enum.GetNames(enumType).Select(value => JsonSerializer.Serialize(value));
    generated.Append("export type ").Append(enumType.Name).Append(" = ")
        .Append(string.Join(" | ", values)).AppendLine(";");
}

generated.AppendLine();
foreach (var type in messages)
{
    var genericParameters = type.IsGenericTypeDefinition
        ? $"<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>"
        : "";
    var typeName = type.Name.Split('`')[0];
    generated.Append("export interface ").Append(typeName).Append(genericParameters).AppendLine(" {");
    foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(property => property.MetadataToken))
    {
        var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ToCamelCase(property.Name);
        var propertyNullability = nullability.Create(property).ReadState;
        var isNullableReference = !property.PropertyType.IsValueType && propertyNullability == NullabilityState.Nullable;
        var isNullableValue = Nullable.GetUnderlyingType(property.PropertyType) is not null;
        var isRequired = property.IsDefined(typeof(RequiredMemberAttribute));
        var optional = (isNullableReference || isNullableValue) && !isRequired;
        var propertyType = ToTypeScript(property.PropertyType);
        generated.Append("  ").Append(JsonSerializer.Serialize(jsonName)).Append(optional ? "?: " : ": ").Append(propertyType).AppendLine(";");
    }
    generated.AppendLine("}");
    generated.AppendLine();
}

var fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
await File.WriteAllTextAsync(fullPath, generated.ToString());
Console.WriteLine($"Generated {enums.Length} enums and {messages.Length} message types: {fullPath}");

string ToTypeScript(Type type)
{
    var underlying = Nullable.GetUnderlyingType(type);
    if (underlying is not null)
        return $"{ToTypeScript(underlying)} | null";
    if (type.IsArray)
        return $"{ToTypeScript(type.GetElementType()!)}[]";
    if (type.IsGenericParameter)
        return type.Name;
    if (type.IsGenericType)
    {
        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(WireMessage<>))
            return $"WireMessage<{ToTypeScript(type.GetGenericArguments()[0])}>";
        if (definition == typeof(Nullable<>))
            return $"{ToTypeScript(type.GetGenericArguments()[0])} | null";
        if (definition == typeof(IReadOnlyList<>) || definition == typeof(List<>))
            return $"{ToTypeScript(type.GetGenericArguments()[0])}[]";
    }
    if (type.IsEnum)
        return type.Name;
    if (type == typeof(string) || type == typeof(char) || type == typeof(DateTimeOffset))
        return "string";
    if (type == typeof(bool))
        return "boolean";
    if (type.IsPrimitive || type == typeof(decimal))
        return "number";
    if (type == typeof(JsonElement) || type == typeof(object))
        return "unknown";
    return type.Name;
}

static string ToCamelCase(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
