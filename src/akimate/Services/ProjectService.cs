using System;
using System.Text.Json;
using akimate.Models;

namespace akimate.Services;

/// <summary>
/// Manages the current project state, serialization, and deserialization.
/// </summary>
public static class ProjectService
{
    /// <summary>
    /// The currently active project. Null if no project is loaded.
    /// </summary>
    public static AnimeProject? Current { get; set; }

    /// <summary>
    /// Creates a new project with the given name and description.
    /// </summary>
    public static AnimeProject CreateNew(string name, string description = "")
    {
        return new AnimeProject
        {
            Name = name,
            Description = description,
            Created = DateTime.Now,
            Modified = DateTime.Now,
            Version = "0.1.0"
        };
    }

    /// <summary>
    /// Serializes a project to JSON.
    /// </summary>
    public static string SaveToJson(AnimeProject project)
    {
        project.Modified = DateTime.Now;
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(project, options);
    }

    /// <summary>
    /// Deserializes a project from JSON. Returns null on failure.
    /// </summary>
    public static AnimeProject? LoadFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AnimeProject>(json);
        }
        catch
        {
            return null;
        }
    }
}
