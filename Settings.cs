using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using System.Threading;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace DirectXHost
{
	static class Settings
	{
		private static readonly string OLD_SAVE_FILE = "props.bin";
		private static readonly string SAVE_FILE = "settings.json";
		[Obsolete]
		[Serializable]
		private struct Data
		{
			public bool overlayClickable;
			public bool savePositions;
			public bool topMost;
			public Rect overlayRect;
			public Rect containerRect;
			public int frameRate;
			public double hostOpacity;
			public Color transparencyKey;
		}
		[DataContract]
		private struct JsonData
		{
			[DataMember] public bool overlayClickable;
			[DataMember] public bool savePositions;
			[DataMember] public bool topMost;
			[DataMember] public Rect overlayRect;
			[DataMember] public Rect containerRect;
			[DataMember] public int frameRate;
			[DataMember] public double hostOpacity;
			[DataMember] public double overlayOpacity;
			[IgnoreDataMember]
			public Color transparencyKey
			{
				get => ColorTranslator.FromHtml(transparencyKeyString);
				set => transparencyKeyString = ColorTranslator.ToHtml(value);//Convert.ToString(value.ToArgb(), 16).Substring(2);
			}
			[DataMember(Name = "transparencyKey")] private string transparencyKeyString;
		}
		private static AutoResetEvent gate = new AutoResetEvent(false);
		private static JsonData data;

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
		public static double hostOpacity
		{
			get => data.hostOpacity;
			set => data.hostOpacity = value;
		}
		public static double overlayOpacity
		{
			get
			{
				if (data.overlayOpacity < 0.25 || data.overlayOpacity > 1)
					data.overlayOpacity = 1;
				return data.overlayOpacity;
			}
			set => data.overlayOpacity = value;
		}

		public static bool isHostTransparent => hostOpacity < 1;

		public static async Task Load() => await Task.Run(() =>
		{
			if (File.Exists(OLD_SAVE_FILE))
			{
				using (FileStream stream = new FileStream(OLD_SAVE_FILE, FileMode.OpenOrCreate))
				{
					var oldData = (Data)new BinaryFormatter().Deserialize(stream);
					// Migrate data for new property
					if (oldData.frameRate == 0)
						oldData.frameRate = 10;
					oldData.topMost = true;
					data = new JsonData
					{
						overlayClickable = oldData.overlayClickable,
						savePositions = oldData.savePositions,
						topMost = oldData.topMost,
						overlayRect = oldData.overlayRect,
						containerRect = oldData.containerRect,
						transparencyKey = oldData.transparencyKey,
						frameRate = oldData.frameRate,
						hostOpacity = oldData.hostOpacity
					};
				}
				File.Delete(OLD_SAVE_FILE);
				Save();
				return;
			}
			if (!File.Exists(SAVE_FILE))
			{
				data = new JsonData
				{
					overlayClickable = true,
					savePositions = false,
					topMost = true,
					overlayRect = new Rect { Size = new Size(Constants.OverlayStartWidth, Constants.OverlayStartHeight), Point = Point.Empty },
					containerRect = new Rect { Size = new Size(Constants.StartWidth, Constants.StartHeight), Point = Point.Empty },
					transparencyKey = Constants.DefaultTransparencyKey,
					frameRate = 10,
					hostOpacity = 1,
					overlayOpacity = 1
				};
				return;
			}
			using (FileStream stream = new FileStream(SAVE_FILE, FileMode.OpenOrCreate))
			{
				data = (JsonData)new DataContractJsonSerializer(typeof(JsonData)).ReadObject(stream);
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
				while (gate.WaitOne() && Running)
				{
					using (FileStream stream = new FileStream(SAVE_FILE, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
					{
						// Truncate the file before writing out the new contents
						stream.SetLength(0);
						new DataContractJsonSerializer(typeof(JsonData)).WriteObject(stream, data);
					}
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
