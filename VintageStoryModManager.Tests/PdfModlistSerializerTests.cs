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
    public void NormalizePayload_RemovesExtraneousWhitespace()
    {
        const string input = "  value1  \nvalue2\r\nvalue3  ";
        string normalized = PdfModlistSerializer.NormalizePayload(input);

        Assert.Equal("value1\nvalue2\nvalue3", normalized);
    }
}
