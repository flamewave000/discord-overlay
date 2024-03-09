using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using System;
using D3D11Device = SharpDX.Direct3D11.Device;
using D2D = SharpDX.Direct2D1;

namespace DiscordOverlay
{
	internal class GraphicsD3D11 : IDisposable
	{
		private enum BufferingType
		{
			DoubleBuffering = 1,
			TripleBuffering = 2
		}

		private SwapChain _swapChain;
		private SwapChainDescription swapChainDescription;
		public SwapChain SwapChain { get { return _swapChain; } }
		private D3D11Device _device;
		private DeviceContext _deviceContext;
		private RenderTargetView _renderTargetView;

		//private Color _bgColor = new SharpDX.Color(/**46, 49, 54, 255/**/51, 51, 51, 255/**/);

		public void Initialise(RenderForm renderForm, bool windowed)
		{
			ModeDescription modeDescription = DescribeBuffer(renderForm.ClientSize.Width, renderForm.ClientSize.Height);
			swapChainDescription = DescribeSwapChain(modeDescription, renderForm, windowed);
			CreateDevice(swapChainDescription);
			// Ignore all windows events
			Factory factory = _swapChain.GetParent<Factory>();
			factory.MakeWindowAssociation(renderForm.Handle, WindowAssociationFlags.IgnoreAll);
			AssignDeviceContext();
			CreateRenderTargetView();
		}

		internal void ResizeGraphics(int width, int height)
		{
			Utilities.Dispose(ref _renderTargetView);
			_swapChain.ResizeBuffers(swapChainDescription.BufferCount, width, height, Format.R8G8B8A8_UNorm, SwapChainFlags.AllowModeSwitch);
			CreateRenderTargetView();
		}

		private ModeDescription DescribeBuffer(int width, int height)
		{
			ModeDescription desc = new ModeDescription()
			{
				Width = width,
				Height = height,
				RefreshRate = new Rational(Constants.RefreshRate, 1),
				Format = Format.R8G8B8A8_UNorm
			};
			return desc;
		}

		private SwapChainDescription DescribeSwapChain(
			ModeDescription modeDescription,
			RenderForm renderForm,
			bool windowed)
		{
			SwapChainDescription desc = new SwapChainDescription()
			{
				ModeDescription = modeDescription,
				SampleDescription = new SampleDescription(1, 0),
				Usage = Usage.RenderTargetOutput,
				BufferCount = (int)BufferingType.DoubleBuffering,
				OutputHandle = renderForm.Handle,
				IsWindowed = windowed
			};
			return desc;
		}

		private void CreateDevice(SwapChainDescription swapChainDescription)
		{
			D3D11Device.CreateWithSwapChain(
				DriverType.Hardware,
				DeviceCreationFlags.BgraSupport,
				new SharpDX.Direct3D.FeatureLevel[] { FeatureLevel.Level_11_0 },
				swapChainDescription,
				out _device,
				out _swapChain);
		}

		private void AssignDeviceContext()
		{
			_deviceContext = _device.ImmediateContext;
		}

		private void CreateRenderTargetView()
		{
			using (Texture2D backBuffer = _swapChain.GetBackBuffer<Texture2D>(0))
			{
				_renderTargetView = new RenderTargetView(_device, backBuffer);
			}
			_deviceContext.OutputMerger.SetRenderTargets(_renderTargetView);
		}

		public void Dispose()
		{
			_swapChain.Dispose();
			_device.Dispose();
			_deviceContext.Dispose();
			_renderTargetView.Dispose();
		}

		public void ClearRenderTargetView()
		{
			_deviceContext.ClearRenderTargetView(_renderTargetView, new SharpDX.Color(Settings.transparencyKey.R, Settings.transparencyKey.G, Settings.transparencyKey.B, Settings.transparencyKey.A));
		}

		public void PresentSwapChain()
		{
			_swapChain.Present(1, PresentFlags.None);
		}
	}
}