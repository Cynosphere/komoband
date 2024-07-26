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
[JsonDerivedType(typeof(GenericEvent))]
[JsonDerivedType(typeof(AddSubscriberPipeEvent), typeDiscriminator: "AddSubscriberPipe")]
[JsonDerivedType(typeof(ReloadConfigurationEvent), typeDiscriminator: "ReloadConfiguration")]
[JsonDerivedType(typeof(ReloadStaticConfigurationEvent), typeDiscriminator: "ReloadStaticConfiguration")]
[JsonDerivedType(typeof(FocusWorkspaceNumberEvent), typeDiscriminator: "FocusWorkspaceNumber")]
[JsonDerivedType(typeof(SendContainerToWorkspaceNumberEvent), typeDiscriminator: "SendContainerToWorkspaceNumber")]
public abstract class Event;
public class GenericEvent : Event {
    public object? Content {get; set;}
}
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
}

public class Containers {
    public Container[] Elements {get; set;}
    public int Focused {get; set;}
    public bool Tile {get; set;}
}

public class Container {
    public string Id {get; set;}
}