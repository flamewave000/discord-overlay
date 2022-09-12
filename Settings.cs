using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using System.Threading;

namespace DirectXHost
{
	static class Settings
	{
		[Serializable]
		private struct Data
		{
			public bool overlayClickable;
			public bool savePositions;
			public bool topMost;
			public Rect overlayRect;
			public Rect containerRect;
			public Color transparencyKey;
			public int frameRate;
		}
		private static AutoResetEvent gate = new AutoResetEvent(false);
		private static Data data;

		public static bool overlayClickable
		{
			get => data.overlayClickable;
			set => data.overlayClickable = value;
		}
		public static bool savePositions
		{
			get => data.savePositions;
			set => data.savePositions = value;
		}
		public static bool topMost
		{
			get => data.topMost;
			set => data.topMost = value;
		}
		public static Rect overlayRect
		{
			get => data.overlayRect;
			set => data.overlayRect = value;
		}
		public static Rect containerRect
		{
			get => data.containerRect;
			set => data.containerRect = value;
		}
		public static Color transparencyKey
		{
			get => data.transparencyKey;
			set => data.transparencyKey = value;
		}
		public static int frameRate
		{
			get => data.frameRate;
			set => data.frameRate = value;
		}

		public static async Task Load() => await Task.Run(() =>
		{
			if (!File.Exists("props.bin"))
			{
				data = new Data
				{
					overlayClickable = true,
					savePositions = false,
					topMost = true,
					overlayRect = new Rect { Size = new Size(Constants.OverlayStartWidth, Constants.OverlayStartHeight), Point = Point.Empty },
					containerRect = new Rect { Size = new Size(Constants.StartWidth, Constants.StartHeight), Point = Point.Empty },
					transparencyKey = Constants.DefaultTransparencyKey,
					frameRate = 10
				};
				return;
			}
			using (FileStream stream = new FileStream("props.bin", FileMode.OpenOrCreate))
			{
				var value = new BinaryFormatter().Deserialize(stream);
				data = (Data)value;
				// Migrate data for new property
				if (data.frameRate == 0)
					data.frameRate = 10;
			}
		});

		public static void Save() => gate.Set();
		public static bool Running { get; private set; }
		public static void Close() { Running = false; gate.Set(); }

		static Settings()
		{
			Running = true;
			Task.Run(() =>
			{
				gate.WaitOne();
				while (Running)
				{
					using (FileStream stream = new FileStream("props.bin", FileMode.OpenOrCreate))
					{
						new BinaryFormatter().Serialize(stream, data);
					}
					gate.WaitOne();
				}
			});
		}

		[Serializable]
		public class Rect
		{
			public Point Point;
			public Size Size;
			public static Rect Empty => new Rect { Size = Size.Empty, Point = Point.Empty };
		}
	}
}
