using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Sherlog.Core.Recipes;

/// <summary>Converts a YAML document to JSON so it can be checked against a JSON Schema.</summary>
internal static class YamlJson
{
    /// <summary>Parses one YAML document. Throws <see cref="YamlException"/> on syntax errors.</summary>
    public static JsonNode? Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode);
    }

    private static JsonNode? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(kv =>
            KeyValuePair.Create(Key(kv.Key), Convert(kv.Value)))),
        YamlSequenceNode seq => new JsonArray(seq.Children.Select(Convert).ToArray()),
        YamlScalarNode scalar => Scalar(scalar),
        _ => throw new YamlException(node.Start, node.End, "Unsupported YAML node."),
    };

    private static string Key(YamlNode key) => key is YamlScalarNode scalar
        ? scalar.Value ?? string.Empty
        : throw new YamlException(key.Start, key.End, "Keys must be plain words, not lists or maps.");

    // Only plain (unquoted) scalars get YAML's core-schema types; quoted ones are always strings.
    private static JsonNode? Scalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }

        if (text is "" or "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        if (text is "true" or "True" or "TRUE")
        {
            return JsonValue.Create(true);
        }

        if (text is "false" or "False" or "FALSE")
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return JsonValue.Create(number);
        }

        return JsonValue.Create(text);
    }
}
