using System.Diagnostics;

#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband;

public partial class BandControl: UserControl {
    private Label labelStatus;
    private Button[] buttons;
    private Deskband deskband = null;
    private Font font;

    public BandControl(Deskband deskband) {
        this.deskband = deskband;
        this.InitializeComponent();

        this.UpdateFont();
    }

    public void InitializeComponent() {
        this.labelStatus = new Label();
        this.SuspendLayout();

        this.Name = "komoband";
        this.BackColor = Color.Black;

        this.labelStatus.Name = "komoband_status";
        if (this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical) {
            this.labelStatus.Size = new Size(30, 192);
        } else {
            this.labelStatus.Size = new Size(192, 30);
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

    private void labelStatus_Paint(object sender, PaintEventArgs e) {
        Label label = (Label) sender;

        var gr = e.Graphics;
        var rect = new Rectangle(0, 0, 192, 30);
        var brush = new SolidBrush(label.ForeColor);

        var format = new StringFormat();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;

        var state = gr.Save();
        gr.ResetTransform();

        if (this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical) {
            gr.RotateTransform(90);
        }

        gr.TranslateTransform(rect.Width / 2, rect.Height / 2, System.Drawing.Drawing2D.MatrixOrder.Append);

        gr.DrawString(label.Text, label.Font, brush, rect, format);

        gr.Restore(state);

        brush.Dispose();
    }

    private void _ShowLabel() {
        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                this.Controls.Remove(button);
                button.Dispose();
            }
            this.buttons = null;
        }

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
        this.font = new Font(this.deskband.config.Font, this.deskband.config.FontSize, FontStyle.Regular, GraphicsUnit.Point, 0);

        if (this.labelStatus != null) {
            this.labelStatus.Font = this.font;
        }
        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                if (button == null) continue;

                button.Font = this.font;
            }
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
        if (this.buttons != null) {
            foreach (Button button in this.buttons) {
                if (button == null) continue;

                this.Controls.Remove(button);
                button.Dispose();
            }
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
            button.FlatAppearance.MouseDownBackColor = this.deskband.config.PressColor.ToColor();
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.FlatAppearance.BorderSize = 0;

            button.Font = this.font;

            button.ForeColor = this.deskband.config.WorkspaceColor.ToColor();
            if (workspace.Containers.Elements.Length == 0) {
                button.ForeColor = this.deskband.config.EmptyWorkspaceColor.ToColor();
            }
            if (i == selected) {
                button.ForeColor = this.deskband.config.ActiveWorkspaceColor.ToColor();
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
        for (int i = 0; i < workspaces.Length; i++) {
            var workspace = workspaces[i];
            if (workspace == null) break;

            var button = this.buttons[i];
            if (button == null) continue;

            button.FlatAppearance.MouseDownBackColor = this.deskband.config.PressColor.ToColor();

            button.ForeColor = this.deskband.config.WorkspaceColor.ToColor();
            if (workspace.Containers.Elements.Length == 0) {
                button.ForeColor = this.deskband.config.EmptyWorkspaceColor.ToColor();
            }
            if (i == selected) {
                button.ForeColor = this.deskband.config.ActiveWorkspaceColor.ToColor();
            }
        }

        this.labelStatus.ForeColor = this.deskband.config.ActiveWorkspaceColor.ToColor();
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
}