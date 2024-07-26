using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Serilog;
using Serilog.Core;
using Serilog.Events;

#pragma warning disable 8603
#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband;

[ComVisible(true)]
[Guid("6249307D-7F13-437B-BF13-13BE692C22A5")]
[CSDeskBand.CSDeskBandRegistration(Name = "komoband", ShowDeskBand = false)]
public class Deskband : CSDeskBand.CSDeskBandWin {
    private static string DATA_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "komoband");

    public Logger Logger;
    public LoggingLevelSwitch loggerSwitch;

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

        if (!Directory.Exists(DATA_PATH))
            Directory.CreateDirectory(DATA_PATH);

        this.loggerSwitch = new LoggingLevelSwitch();
        // TODO: configurable when config exists
        this.loggerSwitch.MinimumLevel = LogEventLevel.Information;
        var logConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(this.loggerSwitch)
            .WriteTo.File(Path.Combine(DATA_PATH, "log.txt"), rollingInterval: RollingInterval.Day);
        this.Logger = logConfig.CreateLogger();
        Log.Logger = Logger;

        Logger.Information("komoband started");

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
                            Logger.Information("Connected to komorebi");
                        }

                        var dataStr = this.ReadString(server);
                        Logger.Verbose("Got data:\n{Data}", dataStr.Replace("\n",""));

                        if (dataStr.StartsWith("{")) {
                            try {
                                var data = JsonSerializer.Deserialize<Notification>(dataStr, jsonOptions);

                                if (data != null) {
                                    if (
                                        data.Event is AddSubscriberPipeEvent ||
                                        data.Event is ReloadConfigurationEvent ||
                                        data.Event is ReloadStaticConfigurationEvent
                                    ) {
                                        Logger.Debug("Got setup event");
                                        var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                        var workspaces = workspacesHolder.Elements;
                                        ((BandControl) control).SetupWorkspaces(workspaces, workspacesHolder.Focused);
                                    } else if (data.Event is FocusWorkspaceNumberEvent) {
                                        Logger.Debug("Got focus workspace event");
                                        var workspaces = data.State.Monitors.Elements[0].Workspaces.Elements;
                                        ((BandControl) control).UpdateWorkspaces(workspaces, ((FocusWorkspaceNumberEvent) data.Event).Content);
                                    } else if (data.Event is SendContainerToWorkspaceNumberEvent) {
                                        Logger.Debug("Got send to workspace event");
                                        var workspacesHolder = data.State.Monitors.Elements[0].Workspaces;
                                        var workspaces = workspacesHolder.Elements;
                                        ((BandControl) control).UpdateWorkspaces(workspaces, workspacesHolder.Focused);
                                    }
                                }
                            } catch (Exception serErr) {
                                Logger.Error(serErr, "Failed to deserialize JSON:");
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
                        Logger.Error(err, "Error in thread:");
                        ((BandControl) control).ShowLabel();
                        try {
                            if (server != null && !this.awaitingReconnect) {
                                if (server.IsConnected) server.Disconnect();
                                this.ConnectPipe();
                            }
                        } catch (Exception err2) {
                            Logger.Error(err2,"Error reconnecting to pipe:");
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
                        Logger.Information("Lost komorebi");
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
            Logger.Error(err, "Error connecting to pipe:");
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
        Logger.Information("komoband closing");
        try {
            pipeThread.Abort();
            watchdog.Abort();
            control.Dispose();
        } catch (Exception err) {
            Logger.Error(err, "Failed to close properly:");
        } finally {
            Logger.Dispose();
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
