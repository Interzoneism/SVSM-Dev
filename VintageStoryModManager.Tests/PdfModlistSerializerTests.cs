using System.Collections.Generic;
using VintageStoryModManager.Core.Models;
using VintageStoryModManager.Core.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PdfModlistSerializerTests
{
    [Fact]
    public void SerializeToBase64_PreservesConfigurationContent()
    {
        var preset = new SerializablePreset
        {
            Name = "Test Modlist",
            IncludeModStatus = true,
            Mods = new List<SerializablePresetModState>
            {
                new()
                {
                    ModId = "examplemod",
                    IsActive = true,
                    ConfigurationFileName = "example.json",
                    ConfigurationContent = "{\"setting\":42}"
                }
            }
        };

        string payload = PdfModlistSerializer.SerializeToBase64(preset);
        string pdfText = $"Header\n###\n{payload}\n###\nFooter";

        Assert.True(
            PdfModlistSerializer.TryExtractModlistJson(pdfText, out string? json, out string? error),
            error);

        Assert.True(
            PdfModlistSerializer.TryDeserializeFromJson(json!, out SerializablePreset? roundTrip, out string? deserializeError),
            deserializeError);

        SerializablePresetModState modState = Assert.Single(roundTrip!.Mods!);
        Assert.Equal("example.json", modState.ConfigurationFileName);
        Assert.Equal("{\"setting\":42}", modState.ConfigurationContent);
    }

    [Fact]
    public void TryExtractModlistJsonFromMetadata_ReadsEncodedPayload()
    {
        var preset = new SerializablePreset
        {
            Name = "Test Modlist",
            IncludeModStatus = true,
            Mods = new List<SerializablePresetModState>
            {
                new()
                {
                    ModId = "examplemod",
                    IsActive = true,
                    ConfigurationFileName = "example.json",
                    ConfigurationContent = "{\"setting\":42}"
                }
            }
        };

        string payload = PdfModlistSerializer.SerializeToBase64(preset);
        string metadataValue = PdfModlistSerializer.CreateModlistMetadataValue(payload);

        Assert.True(
            PdfModlistSerializer.TryExtractModlistJsonFromMetadata(metadataValue, out string? json, out string? error),
            error);

        Assert.True(
            PdfModlistSerializer.TryDeserializeFromJson(json!, out SerializablePreset? roundTrip, out string? deserializeError),
            deserializeError);

        SerializablePresetModState modState = Assert.Single(roundTrip!.Mods!);
        Assert.Equal("example.json", modState.ConfigurationFileName);
        Assert.Equal("{\"setting\":42}", modState.ConfigurationContent);
    }

    [Fact]
    public void SerializeConfigListToBase64_RoundTripsConfigurationEntries()
    {
        var configList = new SerializableConfigList
        {
            Configurations = new List<SerializableModConfiguration>
            {
                new()
                {
                    ModId = "examplemod",
                    FileName = "example.json",
                    Content = "{\"setting\":42}"
                }
            }
        };

        string payload = PdfModlistSerializer.SerializeConfigListToBase64(configList);
        string pdfText = $"Header\n@@@\n{payload}\n@@@\nFooter";

        Assert.True(
            PdfModlistSerializer.TryExtractConfigJson(pdfText, out string? json, out string? error),
            error);

        Assert.True(
            PdfModlistSerializer.TryDeserializeConfigListFromJson(json!, out SerializableConfigList? roundTrip, out string? deserializeError),
            deserializeError);

        SerializableModConfiguration configuration = Assert.Single(roundTrip!.Configurations!);
        Assert.Equal("examplemod", configuration.ModId);
        Assert.Equal("example.json", configuration.FileName);
        Assert.Equal("{\"setting\":42}", configuration.Content);
    }

    [Fact]
    public void TryExtractConfigJsonFromMetadata_ReadsEncodedPayload()
    {
        var configList = new SerializableConfigList
        {
            Configurations = new List<SerializableModConfiguration>
            {
                new()
                {
                    ModId = "examplemod",
                    FileName = "example.json",
                    Content = "{\"setting\":42}"
                }
            }
        };

        string payload = PdfModlistSerializer.SerializeConfigListToBase64(configList);
        string metadataValue = PdfModlistSerializer.CreateConfigMetadataValue(payload);

        Assert.True(
            PdfModlistSerializer.TryExtractConfigJsonFromMetadata(metadataValue, out string? json, out string? error),
            error);

        Assert.True(
            PdfModlistSerializer.TryDeserializeConfigListFromJson(json!, out SerializableConfigList? roundTrip, out string? deserializeError),
            deserializeError);

        SerializableModConfiguration configuration = Assert.Single(roundTrip!.Configurations!);
        Assert.Equal("examplemod", configuration.ModId);
        Assert.Equal("example.json", configuration.FileName);
        Assert.Equal("{\"setting\":42}", configuration.Content);
    }

    [Fact]
    public void TryExtractConfigJsonFromMetadata_HandlesMissingConfigurations()
    {
        string metadataValue = PdfModlistSerializer.CreateConfigMetadataValue(null);

        Assert.True(
            PdfModlistSerializer.TryExtractConfigJsonFromMetadata(metadataValue, out string? json, out string? error));

        Assert.Null(json);
        Assert.Null(error);
    }

    [Fact]
    public void TryExtractConfigJson_IgnoresMissingSection()
    {
        const string pdfText = "Header\n###\n{}\n###\nFooter";

        Assert.True(
            PdfModlistSerializer.TryExtractConfigJson(pdfText, out string? json, out string? error));

        Assert.Null(json);
        Assert.Null(error);
    }

    [Fact]
    public void NormalizePayload_RemovesExtraneousWhitespace()
    {
        const string input = "  value1  \nvalue2\r\nvalue3  ";
        string normalized = PdfModlistSerializer.NormalizePayload(input);

        Assert.Equal("value1\nvalue2\nvalue3", normalized);
    }
}
