using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpuOverlay;

public class AppConfig
{
    [JsonPropertyName("Left")]
    public double Left { get; set; } = 100;

    [JsonPropertyName("Top")]
    public double Top { get; set; } = 100;

    [JsonPropertyName("IsLocked")]
    public bool IsLocked { get; set; } = false;

    [JsonPropertyName("Opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonPropertyName("FontSize")]
    public double FontSize { get; set; } = 36.0;

    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AppConfig();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, Options);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
        }
    }
}
