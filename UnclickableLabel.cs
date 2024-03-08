using System;
using System.Drawing;
using System.Windows.Forms;

namespace DirectXHost
{
	internal class UnclickableLabel : Label
	{
		private const int WM_NCHITTEST = 0x84;
		private const int HTTRANSPARENT = -1;

		protected override void WndProc(ref Message message)
		{
			if (message.Msg == WM_NCHITTEST)
				message.Result = (IntPtr)HTTRANSPARENT;
			else
				base.WndProc(ref message);
		}
		protected override void OnPaint(PaintEventArgs e)
		{
			Rectangle rc = ClientRectangle;
			StringFormat fmt = new StringFormat(StringFormat.GenericTypographic);
			using (var br = new SolidBrush(ForeColor))
			{
				e.Graphics.DrawString(Text, Font, br, rc, fmt);
			}
		}
	}
}
