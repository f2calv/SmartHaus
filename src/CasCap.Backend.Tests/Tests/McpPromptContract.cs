using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CasCap.Tests;

/// <summary>
/// Checks that MCP prompts only name tools that exist, since a prompt naming a removed or renamed tool
/// steers the model towards a call it cannot make.
/// </summary>
public static class McpPromptContract
{
    //Bounds matching against rendered prompt text, which the check does not control.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly string[] ToolVerbs =
    [
        "Get", "Set", "Change", "Turn", "Switch", "Open", "Close", "Send", "Search", "Validate", "Test",
        "Create", "Execute", "Start", "Adjust", "Unlock", "Enable", "List",
    ];

    /// <summary>
    /// Renders every static <see cref="McpServerPromptAttribute"/> method in the assemblies and returns each
    /// verb-prefixed PascalCase word that matches no <see cref="McpServerToolAttribute"/> method, in either its
    /// C# or snake_case form.
    /// </summary>
    /// <param name="assemblies">Assemblies holding the prompt and tool types.</param>
    /// <returns>Entries such as <c>BusSystemMcpPrompts.SendCommand references Send2Bus</c>; empty when all references resolve.</returns>
    public static IReadOnlyList<string> FindMissingToolReferences(IEnumerable<Assembly> assemblies)
    {
        var types = assemblies.SelectMany(a => a.GetTypes()).ToList();
        var toolNames = types
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(m => new[] { m.Name, m.Name.ToSnakeCase() })
            .ToHashSet(StringComparer.Ordinal);

        var verbs = string.Join("|", ToolVerbs.Select(Regex.Escape));
        var referenceRegex = new Regex($@"\b(?:{verbs})[A-Z0-9]\w*\b", RegexOptions.CultureInvariant, RegexTimeout);

        return [.. types
            .Where(t => t.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<McpServerPromptAttribute>() is not null)
            .SelectMany(p => referenceRegex.Matches(Render(p))
                .Select(m => m.Value)
                .Where(name => !toolNames.Contains(name))
                .Select(name => $"{p.DeclaringType!.Name}.{p.Name} references {name}"))
            .Distinct()];
    }

    private static string Render(MethodInfo prompt)
    {
        var arguments = prompt.GetParameters()
            .Select(p => p.HasDefaultValue
                ? p.DefaultValue
                : p.ParameterType == typeof(string) ? "example" : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();
        return prompt.Invoke(null, arguments) switch
        {
            ChatMessage message => message.Text,
            IEnumerable<ChatMessage> messages => string.Join(Environment.NewLine, messages.Select(m => m.Text)),
            string text => text,
            var other => other?.ToString() ?? string.Empty,
        };
    }
}
