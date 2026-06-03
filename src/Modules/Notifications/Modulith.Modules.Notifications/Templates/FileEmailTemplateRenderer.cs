using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;

namespace Modulith.Modules.Notifications.Templates;

public sealed partial class FileEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ConcurrentDictionary<string, TemplateDefinition> templates = new(StringComparer.Ordinal);

    public ErrorOr<RenderedEmailTemplate> Render<TModel>(EmailTemplateId templateId, TModel model)
        where TModel : notnull
    {
        var template = this.GetTemplate(templateId);
        if (template.IsError)
        {
            return template.Errors;
        }

        try
        {
            var values = ModelValues.From(model);
            var html = PlaceholderRegex().Replace(template.Value.Html, match =>
            {
                var variableName = match.Groups["name"].Value;
                if (!template.Value.Schema.Variables.TryGetValue(variableName, out var variable))
                {
                    throw new TemplateRenderException($"Template '{templateId}' contains undeclared placeholder '{variableName}'.");
                }

                if (!values.TryGetValue(variableName, out var value) || value is null)
                {
                    if (variable.Required)
                    {
                        throw new TemplateRenderException($"Template '{templateId}' is missing required variable '{variableName}'.");
                    }

                    return string.Empty;
                }

                return variable.Type switch
                {
                    "string" => WebUtility.HtmlEncode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
                    "url" => FormatUrl(templateId, variableName, value),
                    _ => throw new TemplateRenderException(
                        $"Template '{templateId}' has unsupported variable type '{variable.Type}' for '{variableName}'."),
                };
            });

            return new RenderedEmailTemplate(template.Value.ManifestEntry.Subject, html);
        }
        catch (TemplateRenderException ex)
        {
            return Error.Validation("Notifications.EmailTemplate", ex.Message);
        }
    }

    private static string FormatUrl(EmailTemplateId templateId, string variableName, object value)
    {
        var url = value switch
        {
            Uri uri => uri.ToString(),
            string text when Uri.TryCreate(text, UriKind.Absolute, out _) => text,
            _ => throw new TemplateRenderException(
                $"Template '{templateId}' variable '{variableName}' must be an absolute URL."),
        };

        return WebUtility.HtmlEncode(url);
    }

    private ErrorOr<TemplateDefinition> GetTemplate(EmailTemplateId templateId)
    {
        try
        {
            return this.templates.GetOrAdd(templateId.Value, LoadTemplate);
        }
        catch (TemplateRenderException ex)
        {
            return Error.Validation("Notifications.EmailTemplate", ex.Message);
        }
    }

    private static TemplateDefinition LoadTemplate(string templateId)
    {
        var manifest = ReadJson<TemplateManifest>("templates.manifest.json");
        var entry = manifest.Templates.SingleOrDefault(template => string.Equals(template.Id, templateId, StringComparison.Ordinal))
            ?? throw new TemplateRenderException($"Email template '{templateId}' is not registered in the manifest.");

        var schema = ReadJson<TemplateSchema>(entry.Schema);
        if (!string.Equals(schema.Id, entry.Id, StringComparison.Ordinal))
        {
            throw new TemplateRenderException(
                $"Email template schema id '{schema.Id}' does not match manifest id '{entry.Id}'.");
        }

        var html = ReadResource(entry.Html);
        foreach (Match match in PlaceholderRegex().Matches(html))
        {
            var variableName = match.Groups["name"].Value;
            if (!schema.Variables.ContainsKey(variableName))
            {
                throw new TemplateRenderException(
                    $"Template '{entry.Id}' contains undeclared placeholder '{variableName}'.");
            }
        }

        return new TemplateDefinition(entry, schema, html);
    }

    private static T ReadJson<T>(string fileName)
    {
        var value = JsonSerializer.Deserialize<T>(ReadResource(fileName), jsonOptions);
        return value ?? throw new TemplateRenderException($"Email template JSON file '{fileName}' is empty.");
    }

    private static string ReadResource(string fileName)
    {
        var assembly = typeof(FileEmailTemplateRenderer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($".Templates.Generated.{fileName}", StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new TemplateRenderException($"Email template resource '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new TemplateRenderException($"Email template resource '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"\{\{\s*(?<name>[a-z][a-zA-Z0-9]*)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PlaceholderRegex();

    private sealed record TemplateDefinition(
        TemplateManifestEntry ManifestEntry,
        TemplateSchema Schema,
        string Html);

    private sealed record TemplateManifest(IReadOnlyList<TemplateManifestEntry> Templates);

    private sealed record TemplateManifestEntry(
        string Id,
        string Subject,
        string Html,
        string Schema);

    private sealed record TemplateSchema(
        string Id,
        IReadOnlyDictionary<string, TemplateVariable> Variables);

    private sealed record TemplateVariable(
        string Type,
        bool Required);

    public sealed class TemplateRenderException : Exception
    {
        public TemplateRenderException()
        {
        }

        public TemplateRenderException(string message)
            : base(message)
        {
        }

        public TemplateRenderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private static class ModelValues
    {
        public static Dictionary<string, object?> From<TModel>(TModel model)
            where TModel : notnull
        {
            if (model is IReadOnlyDictionary<string, object?> dictionary)
            {
                return new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
            }

            return model.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToDictionary(
                    property => ToCamelCase(property.Name),
                    property => property.GetValue(model),
                    StringComparer.Ordinal);
        }

        private static string ToCamelCase(string value) =>
            value.Length switch
            {
                0 => value,
                1 => value.ToLowerInvariant(),
                _ => char.ToLowerInvariant(value[0]) + value[1..],
            };
    }
}
