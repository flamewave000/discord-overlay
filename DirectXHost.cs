using System;
using System.Windows.Forms;
using SharpDX.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace DirectXHost
{
	public class DirectXHost : IDisposable
	{
		[DllImport("user32")]
		private static extern int PrintWindow(IntPtr hwnd, IntPtr hdcBlt, UInt32 nFlags);


		private Form _overlayForm;
		private PictureBox _overlayTarget;
		private RenderForm _dxForm;
		private GraphicsD3D11 _graphics;
		private bool UserResized { get; set; }
		private Size ClientSize { get; set; }

		public bool IsDisposed { get; private set; }

		#region Events
		public event EventHandler HostWindowClosed;
		public event EventHandler HostWindowCreated;
		protected void OnHostWindowCreated()
		{
			if (HostWindowCreated != null)
			{
				HostWindowCreated(this._dxForm, new EventArgs());
			}
		}

		protected void OnHostWindowClosed()
		{
			if (HostWindowClosed != null)
			{
				HostWindowClosed(this._dxForm, new EventArgs());
			}
		}
		#endregion

		public DirectXHost()
		{
			try
			{
				InitializeRenderForm();

				_graphics = new GraphicsD3D11();
				_graphics.Initialise(_dxForm, true);
				_dxForm.UserResized += (sender, args) =>
				{
					var renderForm = sender as RenderForm;
					ClientSize = new Size(renderForm.ClientSize.Width, renderForm.ClientSize.Height);
					UserResized = true;
				};
				LoadContent();
			}
			catch (Exception ex)
			{
				MessageBox.Show(string.Format("Exception loading DirectX host window.\r\n{0}", ex.Message), "DirectX Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		public void Exit()
		{
			throw new NotImplementedException();
		}

		public void Run()
		{
			try
			{
				_overlayForm.Show();
				RenderLoop.Run(_dxForm, RenderCallback);
			}
			catch (System.ArgumentException ex)
			{
				MessageBox.Show(string.Format("Exception running DirectX host window.\r\n{0}", ex.Message), "DirectX Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			catch (Exception ex)
			{
				MessageBox.Show(string.Format("Exception running DirectX host window.\r\n{0}", ex.Message), "DirectX Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Set the bitmap object to the size of the screen
		private Bitmap _bmpScreenshot;
		Bitmap bmpScreenshot
		{
			get => _bmpScreenshot;
			set
			{
				_bmpScreenshot = value;
				_overlayTarget.Image = value;
				updateImageBounds();
			}
		}

		Graphics gfxScreenshot;
		private void RenderCallback()
		{
			Update();
			Draw();
			gfxScreenshot = Graphics.FromImage(bmpScreenshot);
			IntPtr dc = gfxScreenshot.GetHdc();
			PrintWindow(_dxForm.Handle, dc, 0);
			gfxScreenshot.ReleaseHdc();
			gfxScreenshot.Dispose();
			_overlayTarget.Invalidate();
		}

		class OverlayForm : Form
		{
			public OverlayForm() : base()
			{
				DoubleBuffered = true;
			}
		}

		bool _drag = false;
		Point _wstart, _mstart;
		Color _bgColor = Color.FromArgb(255, 51, 51, 51);

		private void updateImageBounds()
		{
			_overlayTarget.Size = _dxForm.Size;
			int BorderWidth = (_dxForm.Width - _dxForm.ClientSize.Width) / 2;
			int TitlebarHeight = (_dxForm.Height - _dxForm.ClientSize.Height) - BorderWidth;
			_overlayTarget.Location = new Point(-BorderWidth, -TitlebarHeight);
			_overlayTarget.Width = _dxForm.Width - BorderWidth;
			_overlayTarget.Height = _dxForm.Height - BorderWidth;
			_overlayTarget.Invalidate();
		}
		private void InitializeRenderForm()
		{
			_dxForm = new RenderForm(Constants.WindowTitle);
			_dxForm.HandleCreated += WindowHandleCreated;
			_dxForm.HandleDestroyed += WindowHandleDestroyed;
			_dxForm.ClientSize = new System.Drawing.Size(Constants.StartWidth, Constants.StartHeight);
			_dxForm.MinimumSize = _dxForm.Bounds.Size;
			_dxForm.StartPosition = FormStartPosition.CenterScreen;
			_dxForm.FormBorderStyle = FormBorderStyle.Sizable;
			_dxForm.GotFocus += (s, e) => ShouldShowOverlayFrame(true);
			_dxForm.LostFocus += (s, e) => ShouldShowOverlayFrame(false);
			_dxForm.TopMost = false;

			_overlayForm = new OverlayForm();
			_overlayForm.Text = _overlayForm.Name = "Discord Overlay";
			_overlayForm.ClientSize = new System.Drawing.Size(400, 400);
			_overlayForm.MinimumSize = new System.Drawing.Size(100, 50);
			_overlayForm.StartPosition = FormStartPosition.CenterScreen;
			_overlayForm.BackColor = _bgColor;
			_overlayForm.TransparencyKey = _bgColor;
			_overlayForm.TopMost = true;
			_overlayForm.ShowIcon = false;
			_overlayForm.MinimizeBox = true;
			_overlayForm.MaximizeBox = true;
			_overlayForm.BackgroundImageLayout = ImageLayout.None;
			_overlayForm.FormBorderStyle = FormBorderStyle.Sizable;
			_overlayForm.FormClosing += (s, e) => _dxForm.Close();
			_overlayForm.GotFocus += (s, e) => ShouldShowOverlayFrame(true);
			_overlayForm.LostFocus += (s, e) => ShouldShowOverlayFrame(false);
			_overlayTarget = new PictureBox();
			_overlayTarget.SizeMode = PictureBoxSizeMode.Normal;
			_overlayTarget.BackColor = Color.Red;
			_overlayTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			_overlayForm.Controls.Add(_overlayTarget);

			// Set the bitmap object to the size of the screen
			bmpScreenshot = new Bitmap(_dxForm.Width, _dxForm.Height, PixelFormat.Format32bppArgb);

			_dxForm.MouseDown += (s, e) =>
			{
				_drag = e.Button == MouseButtons.Left;
				_mstart = _dxForm.PointToScreen(e.Location);
				_wstart = _dxForm.Location;
			};
			_dxForm.MouseUp += (s, e) => _drag = e.Button == MouseButtons.Left ? false : _drag;
			_dxForm.MouseMove += (s, e) =>
			{
				if (!_drag) return;
				var newPoint = e.Location;
				var delta = new Point(newPoint.X - _mstart.X, newPoint.Y - _mstart.Y);
				_dxForm.Location = _dxForm.PointToScreen(new Point(_wstart.X + delta.X, _wstart.Y + delta.Y));
			};
		}
		private void ShouldShowOverlayFrame(bool show)
		{
			if (show)
			{
				_overlayForm.BackColor = Color.FromArgb(255, 68, 68, 68);
				_overlayForm.TransparencyKey = Color.FromArgb(255, 68, 68, 68);
				_overlayForm.FormBorderStyle = FormBorderStyle.Sizable;
			}
			else
			{
				_overlayForm.FormBorderStyle = FormBorderStyle.None;
				_overlayForm.BackColor = _bgColor;
				_overlayForm.TransparencyKey = _bgColor;
			}
		}

		private void WindowHandleDestroyed(object sender, EventArgs e)
		{
			this.Dispose();
			this.IsDisposed = true;
			OnHostWindowClosed();
		}

		private void WindowHandleCreated(object sender, EventArgs e)
		{
			OnHostWindowCreated();
		}

		private void LoadContent()
		{
			//To add content to the form if we want to
		}

		public void Update()
		{
			if (!UserResized) return;
			_graphics.ResizeGraphics(ClientSize.Width, ClientSize.Height);
			UserResized = false;
			bmpScreenshot?.Dispose();
			if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
			//// Set the bitmap object to the size of the screen
			bmpScreenshot = new Bitmap(_dxForm.Width, _dxForm.Height, PixelFormat.Format32bppArgb);
		}

		public void Draw()
		{
			_graphics.ClearRenderTargetView();
			_graphics.PresentSwapChain();
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!IsDisposed)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
					_dxForm.Dispose();
				}

				_graphics.Dispose();
				IsDisposed = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		~DirectXHost()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(false);
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			GC.SuppressFinalize(this);
		}
		#endregion

	}
}
