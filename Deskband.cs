using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

#pragma warning disable 8603
#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband;

[ComVisible(true)]
[Guid("6249307D-7F13-437B-BF13-13BE692C22A5")]
[CSDeskBand.CSDeskBandRegistration(Name = "komoband", ShowDeskBand = false)]
public class Deskband : CSDeskBand.CSDeskBandWin {
    private static Control control;
    private static NamedPipeServerStream server;
    private static Thread pipeThread;
    private static Thread watchdog;
    private bool awaitingReconnect = false;
    public bool hasConsole = false;

    private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };

    public Deskband() {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        //this.hasConsole = AllocConsole();

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
                            if (this.hasConsole) Console.WriteLine("Connected to pipe");
                        }

                        var dataStr = this.ReadString(server);
                        if (this.hasConsole) Console.WriteLine($"Got data: {dataStr}");

                        if (dataStr.StartsWith("{")) {
                            try {
                                var data = JsonSerializer.Deserialize<Notification>(dataStr, jsonOptions);

                                if (data != null) {
                                    if (
                                        data.Event is AddSubscriberPipeEvent ||
                                        data.Event is ReloadConfigurationEvent ||
                                        data.Event is ReloadStaticConfigurationEvent
                                    ) {
                                        if (this.hasConsole) Console.WriteLine("Got setup event");
                                        var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                        var workspaces = workspacesHolder.Elements;
                                        ((BandControl) control).SetupWorkspaces(workspaces, workspacesHolder.Focused);
                                    } else if (data.Event is FocusWorkspaceNumberEvent) {
                                        if (this.hasConsole) Console.WriteLine("Got update event");
                                        var workspaces = data.State.Monitors.Elements[0].Workspaces.Elements;
                                        ((BandControl) control).UpdateWorkspaces(workspaces, ((FocusWorkspaceNumberEvent) data.Event).Content);
                                    } else if (data.Event is SendContainerToWorkspaceNumberEvent) {
                                        if (this.hasConsole) Console.WriteLine("Got update event");
                                        var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                        var workspaces = workspacesHolder.Elements;
                                        ((BandControl) control).UpdateWorkspaces(workspaces, workspacesHolder.Focused);
                                    }
                                }
                            } catch (Exception serErr) {
                                if (this.hasConsole) Console.WriteLine($"Failed to deserialize JSON: {serErr.ToString()}");
                            }
                        }
                    }
                } catch (Exception err) {
                    if (err is ThreadAbortException) {
                        if (server != null) {
                            if (server.IsConnected) server.Disconnect();
                            server.Close();
                        }
                    } else {
                        if (this.hasConsole) Console.WriteLine($"Error in thread: {err.Message}");
                        ((BandControl) control).ShowLabel();
                        try {
                            if (server != null && !this.awaitingReconnect) {
                                if (server.IsConnected) server.Disconnect();
                                this.ConnectPipe();
                            }
                        } catch (Exception err2) {
                            if (this.hasConsole) Console.WriteLine($"Error reconnecting to pipe: {err2.Message}");
                        }
                    }
                }
            }
        });
        pipeThread.Start();

        watchdog = new Thread(() => {
            while (true) {
                try {
                    Process[] komorebi = Process.GetProcessesByName("komorebi");
                    if (komorebi.Length == 0 && !this.awaitingReconnect) {
                        if (this.hasConsole) Console.WriteLine("Lost komorebi");
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
                } catch {
                    // noop
                }
            }
        });
        watchdog.Start();

        try {
            this.ConnectPipe();
        } catch (Exception err) {
            if (this.hasConsole) Console.WriteLine($"Error connecting to pipe: {err.Message}");
        }
    }

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

    protected override void DeskbandOnClosed() {
        try {
            pipeThread.Abort();
            watchdog.Abort();
            control.Dispose();
            if (this.hasConsole) {
                bool freed = FreeConsole();
                if (freed) this.hasConsole = false;
            }
        } catch {
            // noop
        }
    }

    protected override Control Control => control;

    // fix for Unsafe complaining about older version
    // https://stackoverflow.com/a/73914330
    Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
        var name = new AssemblyName(args.Name);
        if (name.Name == "System.Runtime.CompilerServices.Unsafe") {
            return typeof(System.Runtime.CompilerServices.Unsafe).Assembly;
        }
        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FreeConsole();
}
