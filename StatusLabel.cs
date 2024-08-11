namespace komoband;

public partial class StatusLabel : Label {
    private Deskband deskband = null!;
    public StatusLabel(Deskband deskband) {
        this.deskband = deskband;
    }

    protected override void OnPaint(PaintEventArgs e) {
        bool vertical = this.deskband.TaskbarInfo.Orientation == CSDeskBand.TaskbarOrientation.Vertical;

        var size = e.Graphics.VisibleClipBounds.Size;

        var gr = e.Graphics;
        var rect = vertical ? new Rectangle(0, 0, (int) size.Height, (int) size.Width) : new Rectangle(0, 0, (int) size.Width, (int) size.Height);
        var brush = new SolidBrush(this.ForeColor);

        var format = new StringFormat();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;

        var state = gr.Save();
        gr.ResetTransform();

        if (vertical) {
            gr.TranslateTransform(size.Width, 0);
            gr.RotateTransform(90);
        }

        gr.DrawString(this.Text, this.Font, brush, rect, format);

        gr.ResetTransform();

        gr.Restore(state);

        brush.Dispose();
    }
}