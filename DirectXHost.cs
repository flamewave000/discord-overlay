using SharpDX.Windows;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
using DirectXHost.Extensions;
using System.Threading;
using System.Reflection;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;

namespace DirectXHost
{
	public class DirectXHost : IDisposable
	{
		public const int GWL_EXSTYLE = -20;
		public const int WS_EX_TOOLWINDOW = 0x00000080;
		public const uint WS_EX_LAYERED = 0x00080000;
		public const uint WS_EX_TRANSPARENT = 0x00000020;

		[DllImport("user32.dll", SetLastError = true)]
		static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

		[DllImport("user32")]
		private static extern int PrintWindow(IntPtr hwnd, IntPtr hdcBlt, UInt32 nFlags);
		[DllImport("user32.dll", SetLastError = true)]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr FindWindow(string lpWindowClass, string lpWindowName);
		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);
		private const int GWL_HWNDPARENT = -8;

		private OverlayForm _overlayForm;
		private PictureBox _overlayTarget;
		private RenderForm _dxForm;
		private GraphicsD3D11 _graphics;
		private Label _overlayHelpPrompt;
		private Stopwatch stopWatch = new Stopwatch();
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


		public void Exit()
		{
			throw new NotImplementedException();
		}

		public async Task Run()
		{
			try
			{
				await Settings.Load();
				InitializeRenderForm();

				_graphics = new GraphicsD3D11();
				_graphics.Initialise(_dxForm, true);
				_dxForm.UserResized += (sender, args) =>
				{
					var renderForm = sender as RenderForm;
					ClientSize = new Size(renderForm.ClientSize.Width, renderForm.ClientSize.Height);
					UserResized = true;
				};
				_overlayForm.Show();
				RenderLoop.Run(_dxForm, RenderCallback, true);
			}
			catch (ArgumentException ex)
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
			stopWatch.Restart();
			if (_dxForm.WindowState != FormWindowState.Minimized)
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
			// Calculate Frame Limiting
			stopWatch.Stop();
			long drawingTime = stopWatch.ElapsedTicks;

			int frameRate = Settings.frameRate;
			if (frameRate > 0)
			{
				int framerateTicks = 10000000 / frameRate;
				long duration = framerateTicks - drawingTime;
				if (duration > 0)
					Thread.Sleep(new TimeSpan(duration));
			}
		}

		class OverlayForm : Form
		{
			private int PreviousStyle;

			public OverlayForm() : base()
			{
				DoubleBuffered = true;
			}
			public void SetFormTransparent()
			{
				PreviousStyle = GetWindowLong(Handle, GWL_EXSTYLE);
				SetWindowLong(Handle, GWL_EXSTYLE, Convert.ToInt32(PreviousStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));
			}

			public void SetFormNormal()
			{
				SetWindowLong(Handle, GWL_EXSTYLE, Convert.ToInt32(PreviousStyle | WS_EX_LAYERED));
			}
		}
		private bool _drag = false;
		private Point _wstart, _mstart;

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
			SizeF scaledSize;
			_dxForm = new RenderForm(Constants.WindowTitle);
			using (var gfx = _dxForm.CreateGraphics())
			{
				scaledSize = new SizeF(gfx.DpiX / 96, gfx.DpiY / 96);
			}
			_dxForm.HandleCreated += WindowHandleCreated;
			_dxForm.HandleDestroyed += WindowHandleDestroyed;
			_dxForm.MinimumSize = new Size((int)(Constants.StartWidth * scaledSize.Width), (int)(Constants.StartHeight * scaledSize.Height));
			_dxForm.ClientSize = Settings.savePositions ? Settings.containerRect.Size : _dxForm.MinimumSize;
			_dxForm.StartPosition = Settings.savePositions ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
			if (Settings.savePositions) _dxForm.Location = Settings.containerRect.Point;
			if (Settings.hostOpacity == 0) Settings.hostOpacity = 1;
			_dxForm.FormBorderStyle = FormBorderStyle.Sizable;
			_dxForm.TopMost = false;
			_dxForm.HelpRequested += _dxForm_HelpRequested;
			_dxForm.Menu = GetMenu();
			_dxForm.UserResized += (sender, args) => { Settings.containerRect.Size = _dxForm.ClientSize; Settings.Save(); };
			_dxForm.LocationChanged += (sender, args) => { Settings.containerRect.Point = _dxForm.Location; Settings.Save(); };
			_dxForm.AllowTransparency = Settings.isHostTransparent;
			_dxForm.Opacity = Settings.hostOpacity;
			_dxForm.GotFocus += (s, e) => ShouldShowOverlayFrame(true);
			_dxForm.LostFocus += (s, e) => ShouldShowOverlayFrame(false);

			IntPtr hprog = FindWindowEx(FindWindowEx(FindWindow("Discord Overlay", "Program Manager"), IntPtr.Zero, "SHELLDLL_DefView", ""), IntPtr.Zero, "SysListView32", "FolderView");
			SetWindowLong(_dxForm.Handle, GWL_HWNDPARENT, hprog);

			_overlayForm = new OverlayForm();
			_overlayForm.Text = ""; _overlayForm.Name = "Discord Overlay";
			_overlayForm.MinimumSize = new Size(100, 50);
			_overlayForm.ClientSize = Settings.savePositions ? Settings.overlayRect.Size : new Size(Constants.OverlayStartWidth, Constants.OverlayStartHeight);
			_overlayForm.StartPosition = Settings.savePositions ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
			if (Settings.savePositions) _overlayForm.Location = Settings.overlayRect.Point;
			_overlayForm.AllowTransparency = true;
			_overlayForm.BackColor = Settings.transparencyKey;
			_overlayForm.TransparencyKey = Settings.transparencyKey;
			_overlayForm.Opacity = Settings.overlayOpacity;
			_overlayForm.TopMost = Settings.topMost;
			_overlayForm.ShowIcon = false;
			_overlayForm.MinimizeBox = false;
			_overlayForm.MaximizeBox = false;
			_overlayForm.ControlBox = false;
			_overlayForm.BackgroundImageLayout = ImageLayout.None;
			_overlayForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;
			_overlayForm.FormClosing += (s, e) => _dxForm.Close();
			_overlayForm.GotFocus += (s, e) => ShouldShowOverlayFrame(true);
			_overlayForm.LostFocus += (s, e) => ShouldShowOverlayFrame(false);
			_overlayTarget = new PictureBox();
			_overlayTarget.SizeMode = PictureBoxSizeMode.Normal;
			_overlayTarget.BackColor = Settings.transparencyKey;
			_overlayTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			_overlayForm.ResizeEnd += (sender, args) => { Settings.overlayRect.Size = _overlayForm.ClientSize; Settings.Save(); };
			_overlayForm.LocationChanged += (sender, args) => { Settings.overlayRect.Point = _overlayForm.Location; Settings.Save(); };
			_overlayForm.ShowInTaskbar = false;
			_overlayForm.ControlBox = false;

			_overlayHelpPrompt = new UnclickableLabel();
			_overlayHelpPrompt.Text = "Click and drag anywhere in this window to move it";
			_overlayHelpPrompt.TextAlign = ContentAlignment.MiddleCenter;
			_overlayHelpPrompt.Anchor = AnchorStyles.None;
			_overlayHelpPrompt.Dock = DockStyle.Fill;
			_overlayHelpPrompt.Font = new Font(FontFamily.GenericSansSerif, 16, FontStyle.Bold);
			_overlayHelpPrompt.ForeColor = Color.White;
			_overlayForm.Controls.AddRange(new Control[] { _overlayHelpPrompt, _overlayTarget });
			_overlayForm.Shown += async (s, e) =>
			{
				const int FRAMERATE = 25;
				await Task.Delay(1000);
				var alpha = 255.0;
				Stopwatch sw = new Stopwatch();
				while (alpha > 0)
				{
					sw.Restart();
					alpha -= 255.0 / FRAMERATE;
					_overlayHelpPrompt.ForeColor = Color.FromArgb(Math.Max(0, (int)alpha), _overlayHelpPrompt.ForeColor);
					if (alpha <= 0)
					{
						_overlayHelpPrompt.Visible = false;
						break;
					}
					await Task.Delay((1000 / FRAMERATE) - (int)sw.ElapsedMilliseconds);
				}
				sw.Stop();
			};

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

			_overlayTarget.MouseDown += (s, e) =>
			{
				_drag = e.Button == MouseButtons.Left;
				_mstart = _overlayForm.PointToScreen(e.Location);
				_wstart = _overlayForm.Location;
			};
			_overlayTarget.MouseUp += (s, e) => _drag = e.Button == MouseButtons.Left ? false : _drag;
			_overlayTarget.MouseMove += (s, e) =>
			{
				if (!_drag) return;
				var newPoint = e.Location;
				var delta = new Point(newPoint.X - _mstart.X, newPoint.Y - _mstart.Y);
				_overlayForm.Location = _overlayForm.PointToScreen(new Point(_wstart.X + delta.X, _wstart.Y + delta.Y));
			};
		}

		private void _dxForm_HelpRequested(object sender, EventArgs eventArgs)
		{
			MessageBox.Show(_dxForm, @"Overlay Clickable: Enables/Disables ability to interact with the Overlay Window.

Save Window Positions: Saves the Overlay and Container sizes and screen positions.

Transparency Colour: The colour used as a transparency key for hiding the background. Windows does not support an Alpha channel, so it has to use a defined colour to control clipping.

FPS: The rate at which the Discord Overlay refreshes. This is defaulted to 10 times per second to minimize CPU load.

If you have issues with the window positions/sizes, delete the 'props.bin' file that is generated in the program's folder. This will reset the program settings.
", "Help", MessageBoxButtons.OK, MessageBoxIcon.Question);
		}
		private int ColorToBgr(Color color) => (color.B << 16) | (color.G << 8) | color.R;
		private MainMenu GetMenu()
		{
			var overlayClickable = new MenuItem("Overlay Clickable", (i, e) =>
			{
				Settings.overlayClickable = !Settings.overlayClickable;
				(i as MenuItem).Checked = Settings.overlayClickable;
				ShouldShowOverlayFrame(Settings.overlayClickable);
				Settings.Save();
			});
			overlayClickable.Checked = Settings.overlayClickable;
			var savePositions = new MenuItem("Save Positions", (i, e) =>
			{
				Settings.savePositions = !Settings.savePositions;
				(i as MenuItem).Checked = Settings.savePositions;
				Settings.Save();
			});
			savePositions.Checked = Settings.savePositions;
			var topMost = new MenuItem("Top Most", (i, e) =>
			{
				Settings.topMost = !Settings.topMost;
				(i as MenuItem).Checked = Settings.topMost;
				Settings.Save();
			});
			topMost.Checked = Settings.topMost;

			var transparency = new MenuItem("Transparency Colour", (i, e) =>
			{
				var dialog = new ColorDialog();
				dialog.Color = Settings.transparencyKey;
				dialog.SolidColorOnly = true;
				dialog.AnyColor = true;
				dialog.FullOpen = true;
				dialog.CustomColors = new int[] { ColorToBgr(Constants.DefaultTransparencyKey), ColorToBgr(Settings.transparencyKey) };
				if (dialog.ShowDialog(_dxForm) == DialogResult.OK)
				{
					Settings.transparencyKey = dialog.Color;
					Settings.Save();
					ShouldShowOverlayFrame(true);
				}
			});
			var framerate = new MenuItem($"{(Settings.frameRate > 0 ? Settings.frameRate.ToString() : "Unlimited")} FPS", (i, e) =>
			{
				switch (Settings.frameRate)
				{
					case 0: Settings.frameRate = 5; break;
					case 5: Settings.frameRate = 10; break;
					case 10: Settings.frameRate = 20; break;
					case 20: Settings.frameRate = 30; break;
					case 30: Settings.frameRate = 60; break;
					case 60: Settings.frameRate = 120; break;
					case 120: Settings.frameRate = 0; break;
				}
				Settings.Save();
				(i as MenuItem).Text = $"{(Settings.frameRate > 0 ? Settings.frameRate.ToString() : "Unlimited")} FPS";
			});
			var hostOpacity = new MenuItem($"{Settings.hostOpacity * 100}% Host Opacity", (i, e) =>
			{
				if (_dxForm.Opacity != Settings.hostOpacity)
				{
					_dxForm.AllowTransparency = Settings.isHostTransparent;
					_dxForm.Opacity = Settings.hostOpacity;
					return;
				}
				switch (Settings.hostOpacity)
				{
					case 1: Settings.hostOpacity = 0.75; break;
					case 0.75: Settings.hostOpacity = 0.5; break;
					case 0.5: Settings.hostOpacity = 0.25; break;
					case 0.25: Settings.hostOpacity = 1; break;
					default: Settings.hostOpacity = 1.0; break;
				}
				Settings.Save();
				_dxForm.AllowTransparency = Settings.isHostTransparent;
				_dxForm.Opacity = Settings.hostOpacity;
				(i as MenuItem).Text = $"{Settings.hostOpacity * 100}% Host Opacity";
			});
			var overlayOpacity = new MenuItem($"{Settings.overlayOpacity * 100}% Overlay Opacity", (i, e) =>
			{
				if (_overlayForm.Opacity != Settings.overlayOpacity)
				{
					_overlayForm.Opacity = Settings.overlayOpacity;
					return;
				}
				switch (Settings.overlayOpacity)
				{
					case 1: Settings.overlayOpacity = 0.75; break;
					case 0.75: Settings.overlayOpacity = 0.5; break;
					case 0.5: Settings.overlayOpacity = 0.25; break;
					case 0.25: Settings.overlayOpacity = 1; break;
					default: Settings.overlayOpacity = 1.0; break;
				}
				Settings.Save();
				_overlayForm.Opacity = Settings.overlayOpacity;
				(i as MenuItem).Text = $"{Settings.overlayOpacity * 100}% Overlay Opacity";
			});

			var help = new MenuItem("?", _dxForm_HelpRequested);
			var version = new MenuItem($"Version {Application.ProductVersion.Substring(0, Application.ProductVersion.Length - 4)}    |");
			version.Enabled = false;

			var menu = new MainMenu();
			menu.MenuItems.AddRange(new MenuItem[] {
				version,
				new MenuItem("Behaviour", new MenuItem[] { overlayClickable, savePositions, topMost }),
				new MenuItem("Graphics", new MenuItem[] { transparency, framerate, hostOpacity, overlayOpacity }),
				help
			});
			return menu;
		}
		private void ShouldShowOverlayFrame(bool show)
		{
			if (_dxForm.WindowState == FormWindowState.Minimized)
				_dxForm.WindowState = FormWindowState.Normal;
			if (show && Settings.overlayClickable)
			{
				_overlayForm.AllowTransparency = false;
				_overlayForm.Opacity = Settings.overlayOpacity;
				_overlayForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;
				_overlayForm.ControlBox = false;
				_overlayForm.SetFormNormal();
			}
			else
			{
				try
				{
					_overlayForm.AllowTransparency = true;
					_overlayForm.FormBorderStyle = FormBorderStyle.None;
					SetWindowLong(_overlayForm.Handle, GWL_EXSTYLE, GetWindowLong(_overlayForm.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
					_overlayForm.ShowInTaskbar = false;
					_overlayForm.BackColor = Settings.transparencyKey;
					_overlayForm.TransparencyKey = Settings.transparencyKey;
					_overlayForm.SetFormTransparent();
				}
				catch (Exception) { }
			}
		}

		private void WindowHandleDestroyed(object sender, EventArgs e)
		{
			Dispose();
			IsDisposed = true;
			OnHostWindowClosed();
		}

		private void WindowHandleCreated(object sender, EventArgs e)
		{
			OnHostWindowCreated();
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
