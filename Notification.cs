using System.Text.Json.Serialization;

#pragma warning disable 8618
namespace komoband;

public class Notification {
    public Event Event {get; set;}
    public State State {get; set;}
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    IgnoreUnrecognizedTypeDiscriminators = true
)]
[JsonDerivedType(typeof(AddSubscriberPipeEvent), typeDiscriminator: "AddSubscriberPipe")]
[JsonDerivedType(typeof(ReloadConfigurationEvent), typeDiscriminator: "ReloadConfiguration")]
[JsonDerivedType(typeof(ReloadStaticConfigurationEvent), typeDiscriminator: "ReloadStaticConfiguration")]
[JsonDerivedType(typeof(FocusWorkspaceNumberEvent), typeDiscriminator: "FocusWorkspaceNumber")]
[JsonDerivedType(typeof(SendContainerToWorkspaceNumberEvent), typeDiscriminator: "SendContainerToWorkspaceNumber")]
[JsonDerivedType(typeof(FocusChangeEvent), typeDiscriminator: "FocusChange")]
[JsonDerivedType(typeof(CloakEvent), typeDiscriminator: "Cloak")]
[JsonDerivedType(typeof(UncloakEvent), typeDiscriminator: "Uncloak")]
[JsonDerivedType(typeof(ChangeLayoutEvent), typeDiscriminator: "ChangeLayout")]
[JsonDerivedType(typeof(ChangeLayoutCustomEvent), typeDiscriminator: "ChangeLayoutCustom")]
[JsonDerivedType(typeof(CycleLayoutEvent), typeDiscriminator: "CycleLayout")]
[JsonDerivedType(typeof(ToggleMonocleEvent), typeDiscriminator: "ToggleMonocle")]
[JsonDerivedType(typeof(ToggleTilingEvent), typeDiscriminator: "ToggleTiling")]
public class Event;
public class AddSubscriberPipeEvent : Event {
    public string Content {get; set;}
}
public class ReloadConfigurationEvent : Event;
public class ReloadStaticConfigurationEvent : Event {
    public string Content {get; set;}
}
public class FocusWorkspaceNumberEvent : Event {
    public int Content {get; set;}
}
public class SendContainerToWorkspaceNumberEvent : Event {
    public int Content {get; set;}
}
public class FocusChangeEvent : Event;
public class CloakEvent : Event;
public class UncloakEvent : Event;
public class ChangeLayoutEvent : Event {
    public string Content {get; set;}
}
public class ChangeLayoutCustomEvent : Event {
    public string Content {get; set;}
}
public class CycleLayoutEvent : Event {
    public string Content {get; set;}
}
public class ToggleMonocleEvent : Event;
public class ToggleTilingEvent : Event;

public class State {
    public Monitors Monitors {get; set;}
}

public class Monitors {
    public Monitor[] Elements {get; set;}
}

public class Monitor {
    public Workspaces Workspaces {get; set;}
}

public class Workspaces {
    public Workspace[] Elements {get; set;}
    public int Focused {get; set;}
}

public class Workspace {
    public string Name {get; set;}
    public Containers Containers {get; set;}
    public bool Tile {get; set;}
    public Layout Layout {get; set;}
    [JsonPropertyName("monocle_container")]
    public MonocleContainer? MonocleContainer {get; set;}
}

public class Containers {
    public Container[] Elements {get; set;}
    public int Focused {get; set;}
}

public class Container {
    public string Id {get; set;}
}

public class Layout {
    public string? Default {get; set;}
    public CustomLayout[]? Custom {get; set;}
}

public class CustomLayout {
    public string Column {get; set;}
}

public class MonocleContainer {
    public string Id {get; set;}
}