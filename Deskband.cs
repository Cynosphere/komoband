using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Serilog;
using Serilog.Core;
using Serilog.Events;

#pragma warning disable 0168
#pragma warning disable 8603
#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband;

[ComVisible(true)]
[Guid("6249307D-7F13-437B-BF13-13BE692C22A5")]
[CSDeskBand.CSDeskBandRegistration(Name = "komoband", ShowDeskBand = false)]
public class Deskband : CSDeskBand.CSDeskBandWin {
    public Logger Logger;
    public LoggingLevelSwitch loggerSwitch;

    public Config config;

    private static Control control;
    private static NamedPipeServerStream server;
    private static Thread pipeThread;
    private static Thread watchdog;
    private bool awaitingReconnect = false;

    private State lastState;

    private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };

    public Deskband() {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

        if (!Directory.Exists(Constants.DATA_PATH))
            Directory.CreateDirectory(Constants.DATA_PATH);

        this.config = Config.Load();

        this.loggerSwitch = new LoggingLevelSwitch();
        this.loggerSwitch.MinimumLevel = this.config.DebugLogs ? LogEventLevel.Verbose : LogEventLevel.Information;
        var logConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(this.loggerSwitch)
            .WriteTo.File(Path.Combine(Constants.DATA_PATH, "log.txt"));
        this.Logger = logConfig.CreateLogger();
        Log.Logger = Logger;

        Logger.Information("komoband started");

        Options.Title = "komoband";
        Options.ShowTitle = false;

        Options.MinHorizontalSize = new Size(30, 30);
        Options.MaxHorizontalHeight = 30;
        Options.HorizontalSize = new Size(192, 30);

        Options.MinVerticalSize = new Size(30, 30);
        Options.MaxVerticalWidth = 30;
        Options.VerticalSize = new Size(30, 192);

        this.TaskbarInfo.TaskbarOrientationChanged += OrientationChanged;

        control = new BandControl(this);

        var actionReload = new CSDeskBand.ContextMenu.DeskBandMenuAction("Reload Config");
        actionReload.Enabled = true;
        actionReload.Clicked += (sender, args) => {
            config = Config.Load();
            var band = (BandControl) control;

            band.UpdateFont();

            if (this.lastState != null) {
                var workspacesHolder = this.lastState.Monitors.Elements[0].Workspaces;
                var selected = workspacesHolder.Focused;
                var workspaces = workspacesHolder.Elements;

                band.SetupWorkspaces(workspaces, selected);
            }
        };
        Options.ContextMenuItems.Add(actionReload);

        server = new NamedPipeServerStream("komoband", PipeDirection.In, -1);
        pipeThread = new Thread(() => {
            while (true) {
                try {
                    if (server != null && server.CanRead) {
                        if (!server.IsConnected) {
                            try {
                                server.WaitForConnection();
                                Logger.Information("Connected to komorebi");
                            } catch (IOException err) {
                                //Logger.Error(err, "Failed to get connection:");
                                if (server != null) server.Disconnect();
                            }
                        }

                        if (server != null && server.IsConnected) {
                            var dataStr = this.ReadString(server);
                            Logger.Verbose("Got data:\n{Data}", dataStr.Replace("\n",""));

                            if (dataStr.StartsWith("{")) {
                                try {
                                    var data = JsonSerializer.Deserialize<Notification>(dataStr, jsonOptions);

                                    if (data != null) {
                                        this.lastState = data.State;

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
                                        } else if (
                                            data.Event is SendContainerToWorkspaceNumberEvent ||
                                            data.Event is FocusChangeEvent ||
                                            data.Event is CloakEvent ||
                                            data.Event is UncloakEvent ||
                                            data.Event is ChangeLayoutEvent ||
                                            data.Event is ChangeLayoutCustomEvent ||
                                            data.Event is CycleLayoutEvent ||
                                            data.Event is ToggleMonocleEvent ||
                                            data.Event is ToggleTilingEvent
                                        ) {
                                            Logger.Debug("Got event that calls generic update");
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
                    }
                } catch (Exception err) {
                    if (err is ThreadAbortException) {
                        if (server != null) {
                            if (server.IsConnected) {
                                server.WaitForPipeDrain();
                                server.Disconnect();
                            }
                            server.Close();
                        }
                    } else {
                        Logger.Error(err, "Error in thread:");
                        ((BandControl) control).ShowLabel();
                        try {
                            if (server != null && !this.awaitingReconnect) {
                                if (server.IsConnected) {
                                    server.WaitForPipeDrain();
                                    server.Disconnect();
                                }
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
                        this.awaitingReconnect = true;
                        if (server != null) {
                            if (server.IsConnected) {
                                server.WaitForPipeDrain();
                                server.Disconnect();
                            }
                            server.Close();
                            server = null;
                        }
                    } else if (komorebi.Length > 0 && this.awaitingReconnect) {
                        Logger.Information("Found komorebi");
                        if (server == null) {
                            server = new NamedPipeServerStream("komoband", PipeDirection.In, -1);
                        }
                        if (server != null) {
                            this.ConnectPipe();
                            Thread.Sleep(1000);
                            if (server.IsConnected) this.awaitingReconnect = false;
                        }
                    }
                } catch (Exception err) {
                    Logger.Error(err, "Watchdog fail:");
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
        if (stream == null || !stream.IsConnected || !stream.CanRead) return "";

        MemoryStream memoryStream = new MemoryStream();

        byte[] buffer = new byte[4096];
        int lastByte = 0x0;

        do {
            if (stream == null || !stream.IsConnected || !stream.CanRead) return "";
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

    private void OrientationChanged(object sender, CSDeskBand.TaskbarOrientationChangedEventArgs e) {
        if (server != null && server.IsConnected && this.lastState != null) {
            var workspacesHolder = this.lastState.Monitors.Elements[0].Workspaces;
            var workspaces = workspacesHolder.Elements;
            ((BandControl) control).SetupWorkspaces(workspaces, workspacesHolder.Focused);
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
}
