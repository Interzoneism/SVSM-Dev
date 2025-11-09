using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace VintageStoryModManager.Services;

/// <summary>
/// Generates server macro files that can be imported by Vintage Story servers.
/// </summary>
public static class ServerMacroGenerator
{
    private const string DefaultPrivilege = "controlserver";
    private const int MaxCommandsPerMacro = 25;

    /// <summary>
    /// Creates a default macro name using the current UTC timestamp.
    /// </summary>
    public static string CreateDefaultMacroName()
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"svsminstall{timestamp}";
    }

    /// <summary>
    /// Generates a macro file that installs every mod in <paramref name="mods"/> using
    /// <c>/moddb install</c> commands.
    /// </summary>
    /// <param name="filePath">Destination path for the generated JSON file.</param>
    /// <param name="macroName">Name of the macro that will be created.</param>
    /// <param name="mods">Collection of mod identifiers and versions to install.</param>
    /// <param name="description">Optional description to associate with the macro.</param>
    /// <param name="privilege">Privilege required to run the macro. Defaults to <c>controlserver</c>.</param>
    /// <returns>Details about the generated macro.</returns>
    public static ServerMacroGenerationResult CreateInstallMacro(
        string filePath,
        string macroName,
        IEnumerable<(string ModId, string Version)> mods,
        string? description = null,
        string? privilege = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(mods);

        string normalizedMacroName = NormalizeMacroName(
            string.IsNullOrWhiteSpace(macroName) ? CreateDefaultMacroName() : macroName);

        var commands = new List<string>();
        foreach ((string ModId, string Version) mod in mods)
        {
            string? command = ServerCommandBuilder.TryBuildInstallCommand(mod.ModId, mod.Version);
            if (!string.IsNullOrWhiteSpace(command))
            {
                commands.Add(command);
            }
        }

        if (commands.Count == 0)
        {
            return ServerMacroGenerationResult.Empty;
        }

        var macroExports = new List<ServerMacroExport>();
        var macroNames = new List<string>();
        string normalizedPrivilege = string.IsNullOrWhiteSpace(privilege) ? DefaultPrivilege : privilege.Trim();

        int commandIndex = 0;
        int macroIndex = 1;
        while (commandIndex < commands.Count)
        {
            int count = Math.Min(MaxCommandsPerMacro, commands.Count - commandIndex);
            string currentMacroName = macroIndex == 1
                ? normalizedMacroName
                : NormalizeMacroName($"{normalizedMacroName}-{macroIndex}");

            string commandsText = string.Join('\n', commands.Skip(commandIndex).Take(count));
            if (!commandsText.EndsWith('\n'))
            {
                commandsText += '\n';
            }

            macroExports.Add(new ServerMacroExport
            {
                Name = currentMacroName,
                Privilege = normalizedPrivilege,
                Commands = commandsText,
                Description = description,
                CreatedByPlayerUid = string.Empty,
                Syntax = string.Empty
            });

            macroNames.Add(currentMacroName);
            commandIndex += count;
            macroIndex++;
        }

        string json = JsonSerializer.Serialize(macroExports, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json, Encoding.UTF8);

        string[] macroNamesArray = macroNames.ToArray();
        string[] commandList = macroNamesArray
            .Select(name => string.IsNullOrEmpty(name) ? string.Empty : "/" + name)
            .ToArray();

        return new ServerMacroGenerationResult(macroNamesArray, commandList, commands.Count);
    }

    private static string NormalizeMacroName(string macroName)
    {
        ArgumentNullException.ThrowIfNull(macroName);

        var builder = new StringBuilder(macroName.Length);
        foreach (char c in macroName)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        if (builder.Length == 0)
        {
            return CreateDefaultMacroName();
        }

        if (!char.IsLetter(builder[0]))
        {
            builder.Insert(0, 'm');
        }

        return builder.ToString();
    }

    private sealed class ServerMacroExport
    {
        public string Privilege { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Commands { get; set; } = string.Empty;

        public string CreatedByPlayerUid { get; set; } = string.Empty;

        public string Syntax { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public readonly struct ServerMacroGenerationResult
    {
        public static readonly ServerMacroGenerationResult Empty = new(Array.Empty<string>(), Array.Empty<string>(), 0);

        public ServerMacroGenerationResult(
            IReadOnlyList<string> macroNames,
            IReadOnlyList<string> commands,
            int commandCount)
        {
            MacroNames = macroNames ?? Array.Empty<string>();
            Commands = commands ?? Array.Empty<string>();
            CommandCount = commandCount < 0 ? 0 : commandCount;
        }

        public IReadOnlyList<string> MacroNames { get; }

        public int CommandCount { get; }

        public IReadOnlyList<string> Commands { get; }

        public int MacroCount => MacroNames.Count;

        public string MacroName => MacroNames.Count > 0 ? MacroNames[0] : string.Empty;

        public string Command => Commands.Count > 0 ? Commands[0] : string.Empty;

        public bool HasMacro => CommandCount > 0 && MacroNames.Count > 0;
    }
}
