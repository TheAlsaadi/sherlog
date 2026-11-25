using System.Text.Json;
using Sherlog.Core.Diagnosis;

namespace Sherlog.Cli.Output;

/// <summary>Machine-readable output (<c>--json</c>) for scripts, tests and the future MCP server.</summary>
internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void Write<T>(TextWriter w, T value) => w.WriteLine(JsonSerializer.Serialize(value, Options));

    public static object Summary(Diagnosis d) => new
    {
        id = d.Group.Id,
        confidence = d.Confidence.ToString(),
        summary = d.Summary,
        recipe = d.Recipe?.Id,
        count = d.Group.Crashes.Count,
        latest = d.Group.Latest.Time,
        app = d.Group.Latest.AppName,
        crashes = d.Group.Crashes.Select(c => c.Id).ToArray(),
    };

    public static object Detail(Diagnosis d) => new
    {
        id = d.Group.Id,
        confidence = d.Confidence.ToString(),
        summary = d.Summary,
        details = d.Details,
        recipe = d.Recipe?.Id,
        whatWeKnow = d.WhatWeKnow,
        fixes = d.Fixes.Select(f => new { id = f.Id, risk = f.Risk, explain = f.Explain, actions = f.Actions }).ToArray(),
        count = d.Group.Crashes.Count,
        latest = d.Group.Latest.Time,
        crashes = d.Group.Crashes.Select(c => c.Id).ToArray(),
    };
}
