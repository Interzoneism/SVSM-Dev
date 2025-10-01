using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VintageStoryModManager.ViewModels;

public sealed class ModConfigEditorViewModel : ObservableObject
{
    private readonly string _filePath;
    private JsonNode? _rootNode;

    public ModConfigEditorViewModel(string modDisplayName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(modDisplayName))
        {
            throw new ArgumentException("Mod name is required.", nameof(modDisplayName));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Configuration file path is required.", nameof(filePath));
        }

        ModDisplayName = modDisplayName;
        _filePath = Path.GetFullPath(filePath);
        WindowTitle = $"Edit Config - {ModDisplayName}";

        LoadConfiguration();
    }

    public string ModDisplayName { get; }

    public string WindowTitle { get; }

    public string FilePath => _filePath;

    public ObservableCollection<ModConfigNodeViewModel> RootNodes { get; private set; } = new();

    public void Save()
    {
        foreach (ModConfigNodeViewModel node in RootNodes)
        {
            node.ApplyChanges();
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = _rootNode is null ? "null" : _rootNode.ToJsonString(options);
        File.WriteAllText(_filePath, json);
    }

    private void LoadConfiguration()
    {
        using FileStream stream = File.OpenRead(_filePath);
        _rootNode = JsonNode.Parse(stream);

        if (_rootNode is null)
        {
            _rootNode = new JsonObject();
        }

        RootNodes = new ObservableCollection<ModConfigNodeViewModel>(CreateRootNodes(_rootNode));
        OnPropertyChanged(nameof(RootNodes));
    }

    private IEnumerable<ModConfigNodeViewModel> CreateRootNodes(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            return obj
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => CreateNode(pair.Key, pair.Value, value => obj[pair.Key] = value))
                .ToList();
        }

        if (node is JsonArray array)
        {
            return new[]
            {
                CreateArrayNode("(root)", array)
            };
        }

        return new[]
        {
            CreateValueNode("(root)", node, value => _rootNode = value)
        };
    }

    private ModConfigNodeViewModel CreateNode(string name, JsonNode? node, Action<JsonNode?> setter)
    {
        if (node is JsonObject obj)
        {
            var children = new ObservableCollection<ModConfigNodeViewModel>(
                obj.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => CreateNode(pair.Key, pair.Value, value => obj[pair.Key] = value)));
            return new ModConfigObjectNodeViewModel(name, children);
        }

        if (node is JsonArray array)
        {
            return CreateArrayNode(name, array);
        }

        return CreateValueNode(name, node, setter);
    }

    private ModConfigNodeViewModel CreateArrayNode(string name, JsonArray array)
    {
        var children = new ObservableCollection<ModConfigNodeViewModel>();
        for (int i = 0; i < array.Count; i++)
        {
            int index = i;
            children.Add(CreateNode($"[{i}]", array[index], value => array[index] = value));
        }

        return new ModConfigArrayNodeViewModel(name, children, () => array.Count);
    }

    private ModConfigNodeViewModel CreateValueNode(string name, JsonNode? node, Action<JsonNode?> setter)
    {
        return new ModConfigValueNodeViewModel(name, node, setter);
    }
}
