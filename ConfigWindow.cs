using System.Diagnostics;
using System.Drawing.Text;
using System.Numerics;
using System.Reflection;
using System.Text;
using ImGuiNET;
using NativeLibraryLoader;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace komoband;

public class ConfigWindow : IDisposable {
    private Deskband deskband = null!;
    private Sdl2Window window = null!;
    private GraphicsDevice gd = null!;
    private ImGuiRenderer imgui = null!;
    private CommandList cl = null!;

    private readonly Task task;
    private readonly CancellationTokenSource cts;

    private readonly List<string> fonts = new List<string>();
    private ushort[] fontRange = new ushort[] {0x20, 0xffff, 0};

    private List<Vector2> groupPanelLabelStackMin = new List<Vector2>();
    private List<Vector2> groupPanelLabelStackMax = new List<Vector2>();

    private Vector4 colorPress;
    private Vector4 colorActiveWorkspace;
    private Vector4 colorWorkspace;
    private Vector4 colorEmptyWorkspace;
    private Vector4 colorLayout;

    public ConfigWindow(Deskband deskband) {
        this.deskband = deskband;

        using (var fontsCollection = new InstalledFontCollection()) {
            var families = fontsCollection.Families;
            foreach (FontFamily font in families) {
                fonts.Add(font.Name);
            }
        }

        var config = this.deskband.config;
        colorPress = config.PressColor.ToVector4();
        colorActiveWorkspace = config.ActiveWorkspaceColor.ToVector4();
        colorWorkspace = config.WorkspaceColor.ToVector4();
        colorEmptyWorkspace = config.EmptyWorkspaceColor.ToVector4();
        colorLayout = config.LayoutColor.ToVector4();

        this.cts = new CancellationTokenSource();
        this.task = Task.Run(this.Run, this.cts.Token);
    }

    public void Dispose() {
        this.cts.Cancel();
        this.task.Wait();
    }

    private void Run() {
        var logger = this.deskband.Logger;
        try {
            var currentPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            new NativeLibrary(Path.Combine(currentPath, "SDL2.dll"));

            logger.Verbose("ConfigWindow: Start");
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(
                    100,
                    100,
                    860,
                    524,
                    WindowState.Normal,
                    "komoband Config"
                ),
                out this.window,
                out this.gd
            );

            if (this.window == null || this.gd == null) {
                logger.Error("Failed to initialize Veldrid");
                return;
            }

            logger.Verbose("ConfigWindow: Veldrid initialized");


            try {
                this.imgui = new ImGuiRenderer(
                    this.gd,
                    this.gd.MainSwapchain.Framebuffer.OutputDescription,
                    this.window.Width,
                    this.window.Height
                );
                logger.Verbose("ConfigWindow: ImGui renderer initialized");
            } catch (Exception err) {
                logger.Error(err, "Failed to initialize ImGui renderer:");
                return;
            }

            var io = ImGui.GetIO();
            unsafe {
                // disable config
                io.NativePtr->IniFilename = null;

                var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();

                ImFontPtr font;
                fixed (ushort* ptr = &fontRange[0]) {
                    font = io.Fonts.AddFontFromFileTTF(
                        @"C:\Windows\Fonts\tahoma.ttf", //Path.Combine(currentPath, "font", "NotoSans-Medium.ttf"),
                        16,
                        fontConfig,
                        new IntPtr(ptr)
                    );
                }

                var fontConfigJp = ImGuiNative.ImFontConfig_ImFontConfig();
                fontConfigJp->MergeMode = 1;

                io.Fonts.AddFontFromFileTTF(
                    @"C:\Windows\Fonts\msgothic.ttc", //Path.Combine(currentPath, "font", "NotoSansJP-Medium.ttf"),
                    16,
                    fontConfigJp,
                    io.Fonts.GetGlyphRangesJapanese()
                );

                this.imgui.RecreateFontDeviceTexture();

                io.NativePtr->FontDefault = font;
            }

            var style = ImGui.GetStyle();
            style.GrabRounding = 4F;
            style.FrameRounding = 4F;
            style.ChildRounding = 4F;

            this.cl = this.gd.ResourceFactory.CreateCommandList();
            this.gd.SyncToVerticalBlank = true;
            this.window.Resized += this.Resized;

            logger.Verbose("ConfigWindow: Rendering loop starting");
            var stopwatch = Stopwatch.StartNew();
            while (this.window.Exists && !this.cts.IsCancellationRequested) {
                var deltaTime = stopwatch.ElapsedTicks / (float) Stopwatch.Frequency;
                stopwatch.Restart();

                var snapshot = this.window.PumpEvents();
                if (!this.window.Exists) break;

                this.imgui.Update(deltaTime, snapshot);

                try {
                    this.Draw();
                } catch (Exception err) {
                    logger.Error(err, "Failed to draw config window:");
                }

                this.cl.Begin();
                this.cl.SetFramebuffer(this.gd.MainSwapchain.Framebuffer);
                this.cl.ClearColorTarget(0, RgbaFloat.Grey);

                this.imgui.Render(this.gd, this.cl);
                this.cl.End();
                this.gd.SubmitCommands(this.cl);
                this.gd.SwapBuffers(this.gd.MainSwapchain);
            }
            logger.Verbose("ConfigWindow: Cleaning up");

            this.gd.WaitForIdle();

            this.window.Resized -= this.Resized;
            this.imgui.Dispose();
            this.cl.Dispose();
            this.gd.Dispose();
            this.window.Close();

            var config = this.deskband.config;
            config.PressColor.FromVector4(colorPress);
            config.ActiveWorkspaceColor.FromVector4(colorActiveWorkspace);
            config.WorkspaceColor.FromVector4(colorWorkspace);
            config.EmptyWorkspaceColor.FromVector4(colorEmptyWorkspace);
            config.LayoutColor.FromVector4(colorLayout);
            config.Save();
            this.deskband.ApplyConfigUpdate();
        } catch (Exception err) {
            logger.Error(err, "Failed to run ConfigWindow task:");
        }
    }

    private void Resized() {
        this.imgui.WindowResized(this.window.Width, this.window.Height);
        this.gd.MainSwapchain.Resize((uint) this.window.Width, (uint) this.window.Height);
    }

    private void BeginGroupPanel(string name, Vector2 size) {
        ImGui.BeginGroup();

        var cursorPos = ImGui.GetCursorScreenPos();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0F, 0F));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0F, 0F));

        var frameHeight = ImGui.GetFrameHeight();
        ImGui.BeginGroup();

        var effectiveSize = new Vector2(size.X, size.Y);
        if (size.X < 0F) {
            effectiveSize.X = ImGui.GetContentRegionAvail().X;
        }
        ImGui.Dummy(new Vector2(effectiveSize.X, 0F));

        ImGui.Dummy(new Vector2(frameHeight * 0.5F, 0F));
        ImGui.SameLine(0F, 0F);
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(frameHeight * 0.5F, 0F));
        ImGui.SameLine(0F, 0F);
        ImGui.TextUnformatted(name);
        var labelMin = ImGui.GetItemRectMin();
        var labelMax = ImGui.GetItemRectMax();
        ImGui.SameLine(0F, 0F);
        ImGui.Dummy(new Vector2(0F, frameHeight + itemSpacing.Y));
        ImGui.BeginChild($"{name}##child", new Vector2(effectiveSize.X - ImGui.GetStyle().ScrollbarSize, size.Y), ImGuiChildFlags.AutoResizeY);

        ImGui.PopStyleVar(2);

        //var contentRegionMax = ImGui.GetWindowContentRegionMax();
        //contentRegionMax.X -= frameHeight * 0.5F;

        var windowSize = ImGui.GetWindowSize();
        windowSize.X -= frameHeight;

        var itemWidth = ImGui.CalcItemWidth();
        ImGui.PushItemWidth(Math.Max(0F, itemWidth - frameHeight));

        groupPanelLabelStackMin.Add(labelMin);
        groupPanelLabelStackMax.Add(labelMax);
    }

    private void EndGroupPanel() {
        ImGui.PopItemWidth();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0F, 0F));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0F, 0F));

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.EndChild();

        ImGui.EndGroup();

        ImGui.SameLine(0F, 0F);
        ImGui.Dummy(new Vector2(frameHeight * 0.5F, 0F));
        ImGui.Dummy(new Vector2(0F, frameHeight - frameHeight * 0.5F - itemSpacing.Y));

        ImGui.EndGroup();

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        var labelMin = groupPanelLabelStackMin.Last();
        groupPanelLabelStackMin.Remove(labelMin);
        var labelMax = groupPanelLabelStackMax.Last();
        groupPanelLabelStackMax.Remove(labelMax);

        var halfFrame = new Vector2(frameHeight * 0.25F, frameHeight) * 0.5F;
        var frameRectMin = itemMin + halfFrame;
        var frameRectMax = itemMax + new Vector2(halfFrame.X, 0F);
        labelMin.X -= itemSpacing.X;
        labelMax.X += itemSpacing.X;

        for (int i = 0; i < 4; i++) {
            switch (i) {
                case 0:
                    ImGui.PushClipRect(
                        new Vector2(-float.MaxValue, -float.MaxValue),
                        new Vector2(labelMin.X, float.MaxValue),
                        true
                    );
                    break;
                case 1:
                    ImGui.PushClipRect(
                        new Vector2(labelMax.X, -float.MaxValue),
                        new Vector2(float.MaxValue, float.MaxValue),
                        true
                    );
                    break;
                case 2:
                    ImGui.PushClipRect(
                        new Vector2(labelMin.X, -float.MaxValue),
                        new Vector2(labelMax.X, labelMin.Y),
                        true
                    );
                    break;
                case 3:
                    ImGui.PushClipRect(
                        new Vector2(labelMin.X, labelMax.Y),
                        new Vector2(labelMax.X, float.MaxValue),
                        true
                    );
                    break;
            }

            ImGui.GetWindowDrawList().AddRect(
                frameRectMin,
                frameRectMax,
                ImGui.GetColorU32(ImGuiCol.Border)
            );

            ImGui.PopClipRect();
        }

        ImGui.PopStyleVar(2);

        //var contentRegionMax = ImGui.GetWindowContentRegionMax();
        //contentRegionMax.X += frameHeight * 0.5F;

        var windowSize = ImGui.GetWindowSize();
        windowSize.X += frameHeight;

        ImGui.Dummy(new Vector2(0F, 0F));

        ImGui.EndGroup();
    }

    private void Draw() {
        var config = this.deskband.config;

        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.SetNextWindowPos(Vector2.Zero);
        if (ImGui.Begin("##Config", ImGuiWindowFlags.NoDecoration)) {
            var size = ImGui.GetContentRegionAvail();

            {
                this.BeginGroupPanel("General", new Vector2(size.X * 0.5F, size.Y - 32));

                ImGui.SeparatorText("Font");
                var selectedIndex = fonts.FindIndex((s) => s.Equals(config.Font));
                if (ImGui.BeginCombo("Font", fonts[selectedIndex])) {
                    for (int i = 0; i < fonts.Count; i++) {
                        var selected = i == selectedIndex;
                        var name = fonts[i];
                        if (ImGui.Selectable(name, selected)) {
                            selectedIndex = i;
                            config.Font = name;
                        }

                        if (selected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                int fontSize = (int) config.FontSize;
                ImGui.InputInt("Font Size (pt)", ref fontSize);
                config.FontSize = fontSize;

                ImGui.SeparatorText("Colors");

                ImGui.ColorEdit4("Button Pressed", ref colorPress, ImGuiColorEditFlags.AlphaPreviewHalf);

                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.ColorEdit4("Active Workspace", ref colorActiveWorkspace, ImGuiColorEditFlags.AlphaPreviewHalf);
                ImGui.ColorEdit4("Workspace", ref colorWorkspace, ImGuiColorEditFlags.AlphaPreviewHalf);
                ImGui.ColorEdit4("Empty Workspace", ref colorEmptyWorkspace, ImGuiColorEditFlags.AlphaPreviewHalf);

                this.EndGroupPanel();
            }

            ImGui.SameLine();

            {
                this.BeginGroupPanel("Layout Switcher", new Vector2(0, size.Y - 32));

                ImGui.Checkbox("Enabled", ref config.LayoutEnabled);

                ImGui.SeparatorText("Font");
                var selectedIndex = fonts.FindIndex((s) => s.Equals(config.LayoutFont));
                if (ImGui.BeginCombo("Font", fonts[selectedIndex])) {
                    for (int i = 0; i < fonts.Count; i++) {
                        var selected = i == selectedIndex;
                        var name = fonts[i];
                        if (ImGui.Selectable(name, selected)) {
                            selectedIndex = i;
                            config.LayoutFont = name;
                        }

                        if (selected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                int fontSize = (int) config.LayoutFontSize;
                ImGui.InputInt("Font Size (pt)", ref fontSize);
                config.LayoutFontSize = fontSize;

                ImGui.SeparatorText("Colors");
                ImGui.ColorEdit4("Layout Foreground", ref colorLayout, ImGuiColorEditFlags.AlphaPreviewHalf);

                ImGui.SeparatorText("Icons");
                var iconFloating = Encoding.Default.GetBytes(config.LayoutIcons.Floating);
                ImGui.InputText("Floating", iconFloating, 12);
                config.LayoutIcons.Floating = Encoding.UTF8.GetString(iconFloating);

                var iconMonocle = Encoding.Default.GetBytes(config.LayoutIcons.Monocle);
                ImGui.InputText("Monocle", iconMonocle, 12);
                config.LayoutIcons.Monocle = Encoding.UTF8.GetString(iconMonocle);

                var iconCustom = Encoding.Default.GetBytes(config.LayoutIcons.Custom);
                ImGui.InputText("Custom", iconCustom, 12);
                config.LayoutIcons.Custom = Encoding.UTF8.GetString(iconCustom);

                ImGui.Spacing();
                ImGui.Spacing();

                var iconBsp = Encoding.Default.GetBytes(config.LayoutIcons.Bsp);
                ImGui.InputText("BSP", iconBsp, 12);
                config.LayoutIcons.Bsp = Encoding.UTF8.GetString(iconBsp);

                var iconColumns = Encoding.Default.GetBytes(config.LayoutIcons.Columns);
                ImGui.InputText("Columns", iconColumns, 12);
                config.LayoutIcons.Columns = Encoding.UTF8.GetString(iconColumns);

                var iconRows = Encoding.Default.GetBytes(config.LayoutIcons.Rows);
                ImGui.InputText("Rows", iconRows, 12);
                config.LayoutIcons.Rows = Encoding.UTF8.GetString(iconRows);

                var iconVerticalStack = Encoding.Default.GetBytes(config.LayoutIcons.VerticalStack);
                ImGui.InputText("Vertical Stack", iconVerticalStack, 12);
                config.LayoutIcons.VerticalStack = Encoding.UTF8.GetString(iconVerticalStack);

                var iconHorizontalStack = Encoding.Default.GetBytes(config.LayoutIcons.HorizontalStack);
                ImGui.InputText("Horizontal Stack", iconHorizontalStack, 12);
                config.LayoutIcons.HorizontalStack = Encoding.UTF8.GetString(iconHorizontalStack);

                var iconUltrawideVerticalStack = Encoding.Default.GetBytes(config.LayoutIcons.UltrawideVerticalStack);
                ImGui.InputText("Ultrawide Vertical Stack", iconUltrawideVerticalStack, 12);
                config.LayoutIcons.UltrawideVerticalStack = Encoding.UTF8.GetString(iconUltrawideVerticalStack);

                var iconGrid = Encoding.Default.GetBytes(config.LayoutIcons.Grid);
                ImGui.InputText("Grid", iconGrid, 12);
                config.LayoutIcons.Grid = Encoding.UTF8.GetString(iconGrid);

                var iconRightMainVerticalStack = Encoding.Default.GetBytes(config.LayoutIcons.RightMainVerticalStack);
                ImGui.InputText("Right Main Vertical Stack", iconRightMainVerticalStack, 12);
                config.LayoutIcons.RightMainVerticalStack = Encoding.UTF8.GetString(iconRightMainVerticalStack);

                this.EndGroupPanel();
            }
        }
        ImGui.End();
    }
}