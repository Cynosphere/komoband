using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Numerics;

using Serilog;

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

    [JsonInclude] public bool LayoutEnabled = true;
    [JsonInclude] public string LayoutFont = "Segoe UI";
    [JsonInclude] public float LayoutFontSize = 8F;
    [JsonInclude] public ConfigColor LayoutColor = new ConfigColor(222, 219, 235, 255);
    [JsonInclude] public LayoutIcons LayoutIcons = new LayoutIcons();

    [JsonInclude] public bool DebugLogs = false;

    public static Config Load() {
        Config config;
        try {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath))!;
        } catch (Exception err) {
            Log.Warning(err, "Failed to load config file, overwriting with new one:");
            config = new Config();
        }

        config!.Save();

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

    public Vector4 ToVector4() {
        return new Vector4(this.R / 255F, this.G / 255F, this.B / 255F, this.A / 255F);
    }

    public void FromVector4(Vector4 vec) {
        this.R = (int) (vec.X * 255F);
        this.G = (int) (vec.Y * 255F);
        this.B = (int) (vec.Z * 255F);
        this.A = (int) (vec.W * 255F);
    }
}

public class LayoutIcons {
    public string Bsp {get; set;} = "├┬";
    public string Columns {get; set;} = "│││";
    public string Rows {get; set;} = "≡";
    public string VerticalStack {get; set;} = "├─";
    public string HorizontalStack {get; set;} = "┬┬";
    public string UltrawideVerticalStack {get; set;} = "│ ├─";
    public string Grid {get; set;} = "─┼─";
    public string RightMainVerticalStack {get; set;} = "─┤";
    public string Custom {get; set;} = "│C│";
    public string Monocle {get; set;} = "│M│";
    public string Floating {get; set;} = "><>";
}