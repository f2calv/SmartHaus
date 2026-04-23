using Swashbuckle.AspNetCore.SwaggerGen;
using System.Xml.XPath;

namespace CasCap.Swagger;

/// <summary>
/// Swashbuckle operation filter that resolves <c>&lt;inheritdoc cref="…"/&gt;</c> tags
/// in XML documentation files, enabling controller actions to inherit summaries
/// and parameter descriptions from service methods.
/// </summary>
public class InheritDocOperationFilter : IOperationFilter
{
    private readonly Dictionary<string, XPathNavigator> _memberCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="InheritDocOperationFilter"/> class
    /// by scanning all XML documentation files in <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public InheritDocOperationFilter()
    {
        _memberCache = [];

        foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var doc = new XPathDocument(xmlFile);
                var nav = doc.CreateNavigator();
                var members = nav.Select("/doc/members/member");
                while (members.MoveNext())
                {
                    var name = members.Current?.GetAttribute("name", string.Empty);
                    if (!string.IsNullOrEmpty(name))
                        _memberCache.TryAdd(name, members.Current!.Clone());
                }
            }
            catch
            {
                // Skip non-documentation XML files (e.g. KNX group address exports)
            }
        }
    }

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var memberName = GetMemberName(context.MethodInfo);
        if (memberName is null || !_memberCache.TryGetValue(memberName, out var memberNode))
            return;

        var inheritDoc = memberNode.SelectSingleNode("inheritdoc[@cref]");
        if (inheritDoc is null)
            return;

        var cref = inheritDoc.GetAttribute("cref", string.Empty);
        if (string.IsNullOrEmpty(cref) || !_memberCache.TryGetValue(cref, out var targetNode))
            return;

        if (string.IsNullOrEmpty(operation.Summary))
        {
            var summary = targetNode.SelectSingleNode("summary");
            if (summary is not null)
                operation.Summary = CleanXmlText(summary.InnerXml);
        }

        if (string.IsNullOrEmpty(operation.Description))
        {
            var remarks = targetNode.SelectSingleNode("remarks");
            if (remarks is not null)
                operation.Description = CleanXmlText(remarks.InnerXml);
        }

        // Inherit parameter descriptions
        foreach (var param in operation.Parameters ?? [])
        {
            if (!string.IsNullOrEmpty(param.Description))
                continue;

            var paramNode = targetNode.SelectSingleNode($"param[@name='{param.Name}']");
            if (paramNode is not null)
                param.Description = CleanXmlText(paramNode.InnerXml);
        }
    }

    #region private/static helpers

    private static string? GetMemberName(MethodInfo method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType is null)
            return null;

        var typeName = declaringType.FullName?.Replace('+', '.');
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return $"M:{typeName}.{method.Name}";

        var paramTypes = string.Join(",", parameters.Select(p => GetTypeDocId(p.ParameterType)));
        return $"M:{typeName}.{method.Name}({paramTypes})";
    }

    private static string GetTypeDocId(Type type)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition().FullName;
            if (def is null)
                return type.FullName ?? type.Name;

            var tickIndex = def.IndexOf('`');
            var baseName = tickIndex >= 0 ? def[..tickIndex] : def;
            var args = string.Join(",", type.GetGenericArguments().Select(GetTypeDocId));
            return $"{baseName}{{{args}}}";
        }

        return type.FullName ?? type.Name;
    }

    private static string CleanXmlText(string xml) =>
        xml.Trim()
           .Replace("\r\n", " ")
           .Replace("\n", " ")
           .Replace("  ", " ");

    #endregion
}
