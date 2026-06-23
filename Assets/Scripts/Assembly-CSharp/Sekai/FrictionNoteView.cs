using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FrictionNoteView : BaseNoteView
	{
		[SerializeField]
		protected SpriteRenderer centerIconRenderer;

		public override void Move()
		{
			if (note == null)
			{
				return;
			}

			if (note.Progress <= 0f)
			{
				transform.localScale = Vector3.zero;
				return;
			}

			float progress = CalcProgress();
			transform.localScale = Vector3.one * progress;
			transform.localPosition = CalcNotePosition(ref progress);
			UpdateSpritePayload(progress);
			if (centerIconRenderer != null && spriteRenderer != null)
			{
				centerIconRenderer.color = spriteRenderer.color;
			}
		}

		protected override Vector3 CalcNotePosition(ref float progress)
		{
			if (note == null)
			{
				return Vector3.zero;
			}

			Vector2 center = GetLaneCenter(note.LaneStartF, note.LaneEndF);
			Vector2 effectiveSpawnPos = spawnPosition;
			if (note is NoteBase noteBase && noteBase.IsNegativeSpeed)
			{
				// 使用 spawnPosition 关于判定线的对称点作为负流速起点
				effectiveSpawnPos = 2f * center - spawnPosition;
				progress = Mathf.Min(progress, 1f);
			}
			Vector2 position = Vector2.LerpUnclamped(effectiveSpawnPos, center, progress);
			return new Vector3(position.x, position.y, posZ);
		}

		private float CalcProgress()
		{
			if (note == null)
			{
				return 0f;
			}

			float progress = note.ParentNote != null ? Mathf.Min(note.Progress, 1f) : note.Progress;
			return LiveConfig.GetNoteViewProgress(progress);
		}

		public FrictionNoteView()
		{
		}
	}
}
