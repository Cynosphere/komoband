using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Serilog;

#pragma warning disable 8600
#pragma warning disable 8602
namespace komoband;

public class Config : IDisposable {
    [JsonIgnore]
    public static string ConfigPath = Path.Combine(Constants.DATA_PATH, "config.json");

    [JsonInclude] public string Font = "Segoe UI";
    [JsonInclude] public float FontSize = 8F;

    [JsonInclude] public ConfigColor ActiveWorkspaceColor = new ConfigColor(222, 219, 235, 255);
    [JsonInclude] public ConfigColor WorkspaceColor = new ConfigColor(92, 92, 92, 255);
    [JsonInclude] public ConfigColor EmptyWorkspaceColor = new ConfigColor(56, 56, 56, 255);
    [JsonInclude] public ConfigColor PressColor = new ConfigColor(0, 0, 0, 64);

    [JsonInclude] public bool DebugLogs = false;

    public static Config Load() {
        Config config;
        try {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath));
        } catch (Exception err) {
            Log.Warning(err, "Failed to load config file, overwriting with new one:");
            config = new Config();
        }

        config.Save();

        return config;
    }

    public void Save() {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions {
            WriteIndented = true
        }));
    }

    public void Dispose() {
        // skip saving if modified on disk
        try {
            var newConfig = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(ConfigPath))!;
            var oldConfig = JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(this))!;
            if (!JsonNode.DeepEquals(newConfig, oldConfig)) return;
        } catch {
            // noop
        }

        this.Save();
    }
}

public class ConfigColor {
    public int R {get; set;}
    public int G {get; set;}
    public int B {get; set;}
    public int A {get; set;}

    public ConfigColor(int r, int g, int b, int a) {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    public Color ToColor() {
        return Color.FromArgb(this.A, this.R, this.G, this.B);
    }
}