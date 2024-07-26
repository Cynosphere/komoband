using System.Diagnostics;

#pragma warning disable 8618
#pragma warning disable 8625
namespace komoband {
    public partial class BandControl: UserControl {
        private Label labelStatus;
        private Button[] buttons;

        public BandControl(CSDeskBand.CSDeskBandWin w) {
            this.InitializeComponent();
        }

        public void InitializeComponent() {
            this.labelStatus = new Label();
            this.SuspendLayout();

            this.Name = "komoband";
            this.BackColor = Color.Black;

            this.labelStatus.Name = "komoband_status";
            this.labelStatus.AutoSize = true;
            this.labelStatus.Dock = DockStyle.Fill;
            // TODO: configurable font
            this.labelStatus.Font = new Font("Terminus (TTF)", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            this.labelStatus.ForeColor = Color.White;
            this.labelStatus.TextAlign = ContentAlignment.MiddleCenter;
            this.labelStatus.Text = "Waiting for komorebi";
            this.Controls.Add(this.labelStatus);

            this.ResumeLayout(false);
            this.labelStatus.Height = 30;
            this.PerformLayout();
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

        public void SetupWorkspaces(Workspace[] workspaces, int selected) {
            if (this.buttons != null) {
                foreach (Button button in this.buttons) {
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
                button.Dock = DockStyle.Left;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Color.Transparent;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 0, 0, 0);
                button.FlatAppearance.MouseOverBackColor = Color.Transparent;
                button.FlatAppearance.BorderSize = 0;
                // TODO: configurable font
                button.Font = new Font("Terminus (TTF)", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
                // TODO: configurable colors
                button.ForeColor = Color.FromArgb(255, 0x5c, 0x5c, 0x5c);
                if (workspace.Containers.Elements.Length == 0) {
                    button.ForeColor = Color.FromArgb(255, 0x38, 0x38, 0x38);
                }
                if (i == selected) {
                    button.ForeColor = Color.FromArgb(255, 0xde, 0xdb, 0xeb);
                }
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Text = workspace.Name;
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
                    this.PerformLayout();
                });
            } else {
                this.labelStatus.Visible = false;
                this.PerformLayout();
            }
        }

        private void _UpdateWorkspaces(Workspace[] workspaces, int selected) {
            for (int i = 0; i < workspaces.Length; i++) {
                var workspace = workspaces[i];
                if (workspace == null) break;

                var button = this.buttons[i];
                if (button == null) continue;

                button.ForeColor = Color.FromArgb(255, 0x5c, 0x5c, 0x5c);
                if (workspace.Containers.Elements.Length == 0) {
                    button.ForeColor = Color.FromArgb(255, 0x38, 0x38, 0x38);
                }
                if (i == selected) {
                    button.ForeColor = Color.FromArgb(255, 0xde, 0xdb, 0xeb);
                }
            }
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
}