using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable 8603
#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband {
    [ComVisible(true)]
    [Guid("6249307D-7F13-437B-BF13-13BE692C22A5")]
    [CSDeskBand.CSDeskBandRegistration(Name = "komoband", ShowDeskBand = false)]
    public class Deskband : CSDeskBand.CSDeskBandWin {
        private static Control control;
        private static NamedPipeServerStream server;
        private static Thread pipeThread;
        private static Thread watchdog;
        private bool awaitingReconnect = false;

        private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };

        public Deskband() {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            //AllocConsole();

            Options.MinHorizontalSize = new Size(30, 30);
            Options.MaxHorizontalHeight = 30;

            control = new BandControl(this);

            server = new NamedPipeServerStream("komoband", PipeDirection.In, 1);
            pipeThread = new Thread(() => {
                while (true) {
                    try {
                        if (server != null && !this.awaitingReconnect) {
                            if (!server.IsConnected) {
                                server.WaitForConnection();
                                Console.WriteLine("Connected to pipe");
                            }

                            var dataStr = this.ReadString(server);
                            Console.WriteLine($"Got data: {dataStr}");

                            if (dataStr.StartsWith("{")) {
                                try {
                                    var data = JsonSerializer.Deserialize<Message>(dataStr, jsonOptions);

                                    if (data != null) {
                                        if (
                                            data.Event is AddSubscriberPipeEvent ||
                                            data.Event is ReloadConfigurationEvent ||
                                            data.Event is ReloadStaticConfigurationEvent
                                        ) {
                                            Console.WriteLine("Got setup event");
                                            var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                            var workspaces = workspacesHolder.Elements;
                                            ((BandControl) control).SetupWorkspaces(workspaces, workspacesHolder.Focused);
                                        } else if (data.Event is FocusWorkspaceNumberEvent) {
                                            Console.WriteLine("Got update event");
                                            var workspaces = data.State.Monitors.Elements[0].Workspaces.Elements;
                                            ((BandControl) control).UpdateWorkspaces(workspaces, ((FocusWorkspaceNumberEvent) data.Event).Content);
                                        } else if (data.Event is SendContainerToWorkspaceNumberEvent) {
                                            Console.WriteLine("Got update event");
                                            var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                            var workspaces = workspacesHolder.Elements;
                                            ((BandControl) control).UpdateWorkspaces(workspaces, workspacesHolder.Focused);
                                        }
                                    }
                                } catch (Exception serErr) {
                                    Console.WriteLine($"Failed to deserialize JSON: {serErr.ToString()}");
                                }
                            }
                        }
                    } catch (Exception err) {
                        Console.WriteLine($"Error in thread: {err.Message}");
                        ((BandControl) control).ShowLabel();
                        try {
                            if (server != null && !this.awaitingReconnect) {
                                if (server.IsConnected) server.Disconnect();
                                this.ConnectPipe();
                            }
                        } catch (Exception err2) {
                            Console.WriteLine($"Error reconnecting to pipe: {err2.Message}");
                        }
                    }
                }
            });
            pipeThread.Start();

            watchdog = new Thread(() => {
                while (true) {
                    Process[] komorebi = Process.GetProcessesByName("komorebi");
                    if (komorebi.Length == 0 && !this.awaitingReconnect) {
                        Console.WriteLine("Lost komorebi");
                        ((BandControl) control).ShowLabel();
                        if (server != null) {
                            if (server.IsConnected) server.Disconnect();
                            server.Close();
                            server = null;
                        }
                        this.awaitingReconnect = true;
                    } else if (komorebi.Length > 0 && this.awaitingReconnect) {
                        server = new NamedPipeServerStream("komoband", PipeDirection.In, 1);
                        this.ConnectPipe();
                        this.awaitingReconnect = false;
                    }
                }
            });
            watchdog.Start();

            try {
                this.ConnectPipe();
            } catch (Exception err) {
                Console.WriteLine($"Error connecting to pipe: {err.Message}");
            }
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            var name = new AssemblyName(args.Name);
            if (name.Name == "System.Runtime.CompilerServices.Unsafe") {
                return typeof(System.Runtime.CompilerServices.Unsafe).Assembly;
            }
            return null;
        }

        protected override Control Control => control;

        private void ConnectPipe() {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "komorebic.exe";
            startInfo.Arguments = "subscribe-pipe komoband";
            process.StartInfo = startInfo;
            process.Start();
        }

        private string ReadString(PipeStream stream) {
            MemoryStream memoryStream = new MemoryStream();

            byte[] buffer = new byte[4096];
            int lastByte = 0x0;

            do {
                lastByte = stream.ReadByte();
                memoryStream.WriteByte((byte) lastByte);
            } while (lastByte != 0x0A);

            memoryStream.Position = 0;

            using (StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8)) {
                return reader.ReadToEnd();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
    }


    public class Message {
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
}