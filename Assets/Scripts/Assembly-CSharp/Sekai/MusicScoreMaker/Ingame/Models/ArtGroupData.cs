using System;
using MessagePack;
using Sekai.Live;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Models
{
	public enum ArtGroupType
	{
		Text = 0,
		Image = 1
	}

	public enum TextArtFontMode
	{
		Hershey = 0,
		Outline = 1,
		Centerline = 2
	}

	[Serializable]
	[MessagePackObject(false)]
	public sealed class TextArtSettings
	{
		[Key(0)] public string Text = string.Empty;
		[Key(1)] public TextArtFontMode FontMode = TextArtFontMode.Hershey;
		[Key(2)] public string FontAssetHash;
		[Key(3)] public float HorizontalScale = 1f;
		[Key(4)] public float VerticalScale = 1f;
		[Key(5)] public float StrokeWidth = 1f;

		public TextArtSettings Clone()
		{
			return (TextArtSettings)MemberwiseClone();
		}
	}

	public enum ImageArtSliceMode
	{
		VerticalStrips = 0,
		HorizontalContours = 1
	}

	[Serializable]
	[MessagePackObject(false)]
	public sealed class ImageArtSettings
	{
		[Key(0)] public string ImageAssetHash;
		[Key(1)] public int Threshold = 128;
		[Key(2)] public bool LightForeground;
		[Key(3)] public float Width = 12f;
		[Key(4)] public float RowSpacingTicks = 12f;
		[Key(5)] public float AnchorWidth = 0.125f;
		[Key(6)] public bool CropForeground = true;
		[Key(7)] public ImageArtSliceMode SliceMode;
		[Key(8)] public bool AutoEase = true;

		public ImageArtSettings Clone()
		{
			return (ImageArtSettings)MemberwiseClone();
		}
	}

	[Serializable]
	[MessagePackObject(false)]
	public sealed class ArtGroupData
	{
		[Key(0)] public string Id = Guid.NewGuid().ToString("N");
		[Key(1)] public ArtGroupType Type;
		[Key(2)] public string SourceAssetHash;
		[Key(3)] public long OriginTicks;
		[Key(4)] public int OriginLane;
		[Key(5)] public float ScaleX = 1f;
		[Key(6)] public float ScaleY = 1f;
		[Key(7)] public NoteType NoteType = NoteType.Default;
		[Key(8)] public float LineWidth = 1f;
		[Key(9)] public bool IsManuallyEdited;
		[Key(10)] public TextArtSettings TextSettings;
		[Key(11)] public ImageArtSettings ImageSettings;
		[Key(12)] public float OriginLaneOffset;
		[IgnoreMember, Newtonsoft.Json.JsonIgnore] public float LaneOrigin => OriginLane + OriginLaneOffset;

		public ArtGroupData Clone(string id = null)
		{
			return new ArtGroupData
			{
				Id = id ?? Id,
				Type = Type,
				SourceAssetHash = SourceAssetHash,
				OriginTicks = OriginTicks,
				OriginLane = OriginLane,
				OriginLaneOffset = OriginLaneOffset,
				ScaleX = ScaleX,
				ScaleY = ScaleY,
				NoteType = NoteType,
				LineWidth = LineWidth,
				IsManuallyEdited = IsManuallyEdited,
				TextSettings = TextSettings?.Clone(),
				ImageSettings = ImageSettings?.Clone()
			};
		}
	}
}
