
using System.Drawing;

namespace DirectXHost
{
	public static class Constants
	{
		public const string WindowTitle = "Discord Overlay Host";
		public const int StartWidth = 768 + 32;
		public const int StartHeight = 432 + 68;
		public const int RefreshRate = 60;
		public const int OverlayStartWidth = 400;
		public const int OverlayStartHeight = 400;
		public readonly static Color DefaultTransparencyKey = Color.FromArgb(255, 51, 51, 51);
	}
}
