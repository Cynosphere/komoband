using System.Diagnostics;

namespace komoband;

public partial class BandControl: UserControl {
    private Deskband deskband = null!;
    private Label labelStatus = null!;
    private Button[] buttons = null!;
    private Button layoutSwitcher = null!;

    private ToolTip layoutTooltip = null!;

    private MenuItem layoutMenuNextLayout = null!;
    private MenuItem layoutMenuPrevLayout = null!;
    private MenuItem layoutMenuBsp = null!;
    private MenuItem layoutMenuColumns = null!;
    private MenuItem layoutMenuRows = null!;
    private MenuItem layoutMenuVerticalStack = null!;
    private MenuItem layoutMenuHorizontalStack = null!;
    private MenuItem layoutMenuUltrawideVerticalStack = null!;
    private MenuItem layoutMenuGrid = null!;
    private MenuItem layoutMenuRightMainVerticalStack = null!;

    private Font font = null!;
    private Font layoutFont = null!;

    public BandControl(Deskband deskband) {
        this.deskband = deskband;
        this.InitializeComponent();

        this.UpdateFont();
    }

    public void InitializeComponent() {
        this.labelStatus = new StatusLabel(this.deskband);
        this.SuspendLayout();

        this.Name = "komoband";
        this.BackColor = Color.Black;

        this.labelStatus.Name = "komoband_status";
        if (this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical) {
            this.labelStatus.Size = this.deskband.Options.VerticalSize;
        } else {
            this.labelStatus.Size = this.deskband.Options.HorizontalSize;
        }
        this.labelStatus.Dock = DockStyle.Fill;
        this.labelStatus.Font = this.font;
        this.labelStatus.ForeColor = this.deskband.config.ActiveWorkspaceColor.ToColor();
        this.labelStatus.TextAlign = ContentAlignment.MiddleCenter;
        this.labelStatus.Text = "Waiting for komorebi";

        this.Controls.Add(this.labelStatus);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void _ShowLabel() {
        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                this.Controls.Remove(button);
                if (button == null) continue;

                button.Dispose();
            }
            this.buttons = null!;
        }

        if (this.layoutSwitcher != null) this.layoutSwitcher.Dispose();

        this.labelStatus.Visible = true;
        this.PerformLayout();
    }

    public void ShowLabel() {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this._ShowLabel();
            });
        } else {
            this._ShowLabel();
        }
    }

    public void _UpdateFont() {
        var config = this.deskband.config;

        this.font = new Font(config.Font, config.FontSize, FontStyle.Regular, GraphicsUnit.Point, 0);
        this.layoutFont = new Font(config.LayoutFont, config.LayoutFontSize, FontStyle.Regular, GraphicsUnit.Point, 0);

        if (this.labelStatus != null) {
            this.labelStatus.Font = this.font;
        }
        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                if (button == null) continue;

                button.Font = this.font;
            }
        }
        if (this.layoutSwitcher != null) {
            this.layoutSwitcher.Font = this.layoutFont;
        }
        this.Invalidate();
    }

    public void UpdateFont() {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this._UpdateFont();
            });
        } else {
            this._UpdateFont();
        }
    }

    public void SetupWorkspaces(Workspace[] workspaces, int selected) {
        var config = this.deskband.config;

        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                this.Controls.Remove(button);
                if (button == null) continue;

                button.Dispose();
            }
        }

        if (this.layoutSwitcher != null) {
            this.Controls.Remove(this.layoutSwitcher);
            this.layoutSwitcher.Dispose();
        }

        if (this.deskband.config.LayoutEnabled) {
            CreateLayoutSwitcher(workspaces[selected]);
        }

        int workspaceCount = workspaces.Length;
        this.buttons = new Button[workspaceCount];
        for (int i = workspaceCount - 1; i >= 0; i--) {
            var workspace = workspaces[i];
            if (workspace == null) break;

            var button = new Button();
            button.Name = $"komoband_workspace_{i}";

            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.AutoEllipsis = false;

            button.Padding = new Padding(0);

            button.Dock = this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical ? DockStyle.Top : DockStyle.Left;

            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.Transparent;
            button.FlatAppearance.MouseDownBackColor = config.PressColor.ToColor();
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.FlatAppearance.BorderSize = 0;

            button.Font = this.font;

            button.ForeColor = config.WorkspaceColor.ToColor();
            if (workspace.Containers.Elements.Length == 0) {
                button.ForeColor = config.EmptyWorkspaceColor.ToColor();
            }
            if (i == selected) {
                button.ForeColor = config.ActiveWorkspaceColor.ToColor();
            }

            button.TextAlign = ContentAlignment.MiddleCenter;

            var name = workspace.Name;
            if (name == null || name == "") {
                name = $"{i + 1}";
            }

            button.Text = name;

            button.Click += (sender, e) => {
                int workspaceId = Int32.Parse(((Button) sender).Name.Replace("komoband_workspace_", ""));
                SwitchWorkspace(workspaceId);
            };

            this.buttons[i] = button;

            if (this.InvokeRequired) {
                this.BeginInvoke((MethodInvoker)delegate() {
                    this.Controls.Add(button);
                });
            } else {
                this.Controls.Add(button);
            }
        }

        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this.labelStatus.Visible = false;
                this.Invalidate();
            });
        } else {
            this.labelStatus.Visible = false;
            this.Invalidate();
        }
    }

    private void _UpdateWorkspaces(Workspace[] workspaces, int selected) {
        var config = this.deskband.config;

        for (int i = 0; i < workspaces.Length; i++) {
            var workspace = workspaces[i];
            if (workspace == null) break;

            var button = this.buttons[i];
            if (button == null) continue;

            button.FlatAppearance.MouseDownBackColor = config.PressColor.ToColor();

            button.ForeColor = config.WorkspaceColor.ToColor();
            if (workspace.Containers.Elements.Length == 0) {
                button.ForeColor = config.EmptyWorkspaceColor.ToColor();
            }
            if (i == selected) {
                button.ForeColor = config.ActiveWorkspaceColor.ToColor();
            }
        }

        var currentWorkspace = workspaces[selected];
        if (this.layoutSwitcher != null) {
            if (!config.LayoutEnabled) {
                this.layoutSwitcher.Dispose();
            } else {
                this.layoutSwitcher.ForeColor = config.LayoutColor.ToColor();
                this.layoutSwitcher.Text = GetLayoutIcon(currentWorkspace);
                if (this.layoutTooltip != null) this.layoutTooltip.SetToolTip(this.layoutSwitcher, GetLayoutName(currentWorkspace));
                UpdateCheckedMenuItem(currentWorkspace);
            }
        } else if (this.layoutSwitcher == null && config.LayoutEnabled) {
            CreateLayoutSwitcher(currentWorkspace);
        }

        this.labelStatus.ForeColor = config.ActiveWorkspaceColor.ToColor();
        this.Invalidate();
    }

    public void UpdateWorkspaces(Workspace[] workspaces, int selected) {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this._UpdateWorkspaces(workspaces, selected);
            });
        } else {
            this._UpdateWorkspaces(workspaces, selected);
        }
    }

    private void SwitchWorkspace(int index) {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.FileName = "komorebic.exe";
        startInfo.Arguments = $"focus-workspace {index}";
        process.StartInfo = startInfo;
        process.Start();
    }

    private void _CreateLayoutSwitcher(Workspace currentWorkspace) {
        if (this.layoutTooltip != null) this.layoutTooltip.Dispose();

        var config = this.deskband.config;

        this.layoutSwitcher = new Button();
        this.layoutSwitcher.Name = "komoband_layout";

        this.layoutSwitcher.AutoSize = true;
        this.layoutSwitcher.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.layoutSwitcher.AutoEllipsis = false;

        this.layoutSwitcher.Padding = new Padding(0);

        this.layoutSwitcher.Dock = this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical ? DockStyle.Top : DockStyle.Left;

        this.layoutSwitcher.FlatStyle = FlatStyle.Flat;
        this.layoutSwitcher.BackColor = Color.Transparent;
        this.layoutSwitcher.FlatAppearance.MouseDownBackColor = config.PressColor.ToColor();
        this.layoutSwitcher.FlatAppearance.MouseOverBackColor = Color.Transparent;
        this.layoutSwitcher.FlatAppearance.BorderSize = 0;

        this.layoutSwitcher.Font = this.layoutFont;
        this.layoutSwitcher.ForeColor = config.LayoutColor.ToColor();
        this.layoutSwitcher.TextAlign = ContentAlignment.MiddleCenter;

        this.layoutSwitcher.Text = GetLayoutIcon(currentWorkspace);

        this.layoutTooltip = new ToolTip();
        this.layoutTooltip.ShowAlways = true;
        this.layoutTooltip.SetToolTip(this.layoutSwitcher, GetLayoutName(currentWorkspace));

        this.layoutSwitcher.Click += (sender, e) => {
            CycleLayout();
        };

        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this.layoutSwitcher.ContextMenu = CreateLayoutMenu(currentWorkspace);
            });
        } else {
            this.layoutSwitcher.ContextMenu = CreateLayoutMenu(currentWorkspace);
        }

        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this.Controls.Add(this.layoutSwitcher);
            });
        } else {
            this.Controls.Add(this.layoutSwitcher);
        }
    }

    private void CreateLayoutSwitcher(Workspace currentWorkspace) {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this._CreateLayoutSwitcher(currentWorkspace);
            });
        } else {
            this._CreateLayoutSwitcher(currentWorkspace);
        }
    }

    private string GetLayoutIcon(Workspace workspace) {
        var icons = this.deskband.config.LayoutIcons;

        if (!workspace.Tile) {
            return icons.Floating;
        }
        if (workspace.MonocleContainer != null) {
            return icons.Monocle;
        }
        if (workspace.Layout.Custom != null) {
            return icons.Custom;
        }

        switch (workspace.Layout.Default) {
            case "BSP":
                return icons.Bsp;
            case "Columns":
                return icons.Columns;
            case "Rows":
                return icons.Rows;
            case "VerticalStack":
                return icons.VerticalStack;
            case "HorizontalStack":
                return icons.HorizontalStack;
            case "UltrawideVerticalStack":
                return icons.UltrawideVerticalStack;
            case "Grid":
                return icons.Grid;
            case "RightMainVerticalStack":
                return icons.RightMainVerticalStack;
            default:
                return "???";
        }
    }

    private string GetLayoutName(Workspace workspace) {
        if (!workspace.Tile) {
            return "Floating (Tiling disabled)";
        }

        string name = "";

        switch (workspace.Layout.Default) {
            case "BSP":
                name = "BSP";
                break;
            case "Columns":
                name = "Columns";
                break;
            case "Rows":
                name = "Rows";
                break;
            case "VerticalStack":
                name = "Vertical Stack";
                break;
            case "HorizontalStack":
                name = "Horizontal Stack";
                break;
            case "UltrawideVerticalStack":
                name = "Ultrawide Vertical Stack";
                break;
            case "Grid":
                name = "Grid";
                break;
            case "RightMainVerticalStack":
                name = "Right Main Vertical Stack";
                break;
            default:
                name = "Unknown";
                break;
        }

        if (workspace.Layout.Custom != null) {
            name = "Custom";
        }

        if (workspace.MonocleContainer != null) {
            name += " (Monocle)";
        }

        return name;
    }

    private ContextMenu CreateLayoutMenu(Workspace currentWorkspace) {
        if (this.layoutMenuNextLayout != null) this.layoutMenuNextLayout.Dispose();
        if (this.layoutMenuPrevLayout != null) this.layoutMenuPrevLayout.Dispose();
        if (this.layoutMenuBsp != null) this.layoutMenuBsp.Dispose();
        if (this.layoutMenuColumns != null) this.layoutMenuColumns.Dispose();
        if (this.layoutMenuRows != null) this.layoutMenuRows.Dispose();
        if (this.layoutMenuVerticalStack != null) this.layoutMenuVerticalStack.Dispose();
        if (this.layoutMenuHorizontalStack != null) this.layoutMenuHorizontalStack.Dispose();
        if (this.layoutMenuUltrawideVerticalStack != null) this.layoutMenuUltrawideVerticalStack.Dispose();
        if (this.layoutMenuGrid != null) this.layoutMenuGrid.Dispose();
        if (this.layoutMenuRightMainVerticalStack != null) this.layoutMenuRightMainVerticalStack.Dispose();

        this.layoutMenuNextLayout = new MenuItem("Next Layout", (sender, e) =>{
            CycleLayout();
        });
        this.layoutMenuPrevLayout = new MenuItem("Previous Layout", (sender, e) =>{
            CycleLayout(true);
        });

        this.layoutMenuBsp = new MenuItem("BSP", LayoutMenuClick);
        this.layoutMenuBsp.RadioCheck = true;
        this.layoutMenuBsp.Name = "bsp";

        this.layoutMenuColumns = new MenuItem("Columns", LayoutMenuClick);
        this.layoutMenuColumns.RadioCheck = true;
        this.layoutMenuColumns.Name = "columns";

        this.layoutMenuRows = new MenuItem("Rows", LayoutMenuClick);
        this.layoutMenuRows.RadioCheck = true;
        this.layoutMenuRows.Name = "rows";

        this.layoutMenuVerticalStack = new MenuItem("Vertical Stack", LayoutMenuClick);
        this.layoutMenuVerticalStack.RadioCheck = true;
        this.layoutMenuVerticalStack.Name = "vertical-stack";

        this.layoutMenuHorizontalStack = new MenuItem("Horizontal Stack", LayoutMenuClick);
        this.layoutMenuHorizontalStack.RadioCheck = true;
        this.layoutMenuHorizontalStack.Name = "horizontal-stack";

        this.layoutMenuUltrawideVerticalStack = new MenuItem("Ultrawide Vertical Stack", LayoutMenuClick);
        this.layoutMenuUltrawideVerticalStack.RadioCheck = true;
        this.layoutMenuUltrawideVerticalStack.Name = "ultrawide-vertical-stack";

        this.layoutMenuGrid = new MenuItem("Grid", LayoutMenuClick);
        this.layoutMenuGrid.RadioCheck = true;
        this.layoutMenuGrid.Name = "grid";

        this.layoutMenuRightMainVerticalStack = new MenuItem("Right Main Vertical Stack", LayoutMenuClick);
        this.layoutMenuRightMainVerticalStack.RadioCheck = true;
        this.layoutMenuRightMainVerticalStack.Name = "right-main-vertical-stack";

        UpdateCheckedMenuItem(currentWorkspace);

        return new ContextMenu(new[]{
            this.layoutMenuNextLayout,
            this.layoutMenuPrevLayout,
            new MenuItem("-"),
            this.layoutMenuBsp,
            this.layoutMenuColumns,
            this.layoutMenuRows,
            this.layoutMenuVerticalStack,
            this.layoutMenuHorizontalStack,
            this.layoutMenuUltrawideVerticalStack,
            this.layoutMenuGrid,
            this.layoutMenuRightMainVerticalStack,
        });
    }

    private void UncheckAllMenuItems() {
        this.layoutMenuBsp.Checked = false;
        this.layoutMenuColumns.Checked = false;
        this.layoutMenuRows.Checked = false;
        this.layoutMenuVerticalStack.Checked = false;
        this.layoutMenuHorizontalStack.Checked = false;
        this.layoutMenuUltrawideVerticalStack.Checked = false;
        this.layoutMenuGrid.Checked = false;
        this.layoutMenuRightMainVerticalStack.Checked = false;
    }

    private void _UpdateCheckedMenuItem(Workspace currentWorkspace) {
        if (
            this.layoutMenuBsp == null ||
            this.layoutMenuColumns == null ||
            this.layoutMenuRows == null ||
            this.layoutMenuVerticalStack == null ||
            this.layoutMenuHorizontalStack == null ||
            this.layoutMenuUltrawideVerticalStack == null ||
            this.layoutMenuGrid == null ||
            this.layoutMenuRightMainVerticalStack == null
        ) return;

        UncheckAllMenuItems();

        switch (currentWorkspace.Layout.Default) {
            case "BSP":
                this.layoutMenuBsp.Checked = true;
                break;
            case "Columns":
                this.layoutMenuColumns.Checked = true;
                break;
            case "Rows":
                this.layoutMenuRows.Checked = true;
                break;
            case "VerticalStack":
                this.layoutMenuVerticalStack.Checked = true;
                break;
            case "HorizontalStack":
                this.layoutMenuHorizontalStack.Checked = true;
                break;
            case "UltrawideVerticalStack":
                this.layoutMenuUltrawideVerticalStack.Checked = true;
                break;
            case "Grid":
                this.layoutMenuGrid.Checked = true;
                break;
            case "RightMainVerticalStack":
                this.layoutMenuRightMainVerticalStack.Checked = true;
                break;
        }
    }

    private void UpdateCheckedMenuItem(Workspace currentWorkspace) {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)delegate() {
                this._UpdateCheckedMenuItem(currentWorkspace);
            });
        } else {
            this._UpdateCheckedMenuItem(currentWorkspace);
        }
    }

    private void LayoutMenuClick(Object sender, EventArgs e) {
        MenuItem item = (MenuItem) sender;

        UncheckAllMenuItems();

        item.Checked = true;
        ChangeLayout(item.Name);
    }

    private void CycleLayout(bool prev = false) {
        string dir = prev ? "previous" : "next";

        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.FileName = "komorebic.exe";
        startInfo.Arguments = $"cycle-layout {dir}";
        process.StartInfo = startInfo;
        process.Start();
    }

    private void ChangeLayout(string layout) {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.FileName = "komorebic.exe";
        startInfo.Arguments = $"change-layout {layout}";
        process.StartInfo = startInfo;
        process.Start();
    }
}
