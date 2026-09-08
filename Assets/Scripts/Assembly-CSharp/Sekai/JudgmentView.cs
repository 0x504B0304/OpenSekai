using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class JudgmentView : MonoBehaviour
	{
		[SerializeField]
		private Sprite miss;

		[SerializeField]
		private Sprite bad;

		[SerializeField]
		private Sprite good;

		[SerializeField]
		private Sprite great;

		[SerializeField]
		private Sprite perfect;

		[SerializeField]
		private Sprite justPerfect;

		[SerializeField]
		private Sprite auto;

		[SerializeField]
		private SpriteRenderer spriteRenderer;

		private Dictionary<NoteResult, Sprite> sprites;

		private float time;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			InitSprites();
			Hide();
		}

		private void InitSprites()
		{
			bool useFakePerfect = LiveSettingData.LoadFromStorage()?.UsesAutoFakePerfectMode ?? false;
			Sprite autoSprite = useFakePerfect ? perfect : auto;
			sprites = new Dictionary<NoteResult, Sprite>
			{
				{ NoteResult.Miss, miss },
				{ NoteResult.Bad, bad },
				{ NoteResult.Good, good },
				{ NoteResult.Great, great },
				{ NoteResult.Perfect, perfect },
				{ NoteResult.JustPerfect, perfect },
				{ NoteResult.Auto, autoSprite }
			};
		}

		public void Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (sprites == null)
			{
				InitSprites();
			}
			if (sprites != null && sprites.TryGetValue(judgeInfo.result, out Sprite sprite) && spriteRenderer != null)
			{
				spriteRenderer.sprite = sprite;
				time = 0f;
				spriteRenderer.transform.localScale = Vector3.zero;
			}
		}

		private void Update()
		{
			if (spriteRenderer == null)
			{
				return;
			}
			if (time > 0.3f)
			{
				spriteRenderer.transform.localScale = Vector3.zero;
				return;
			}
			float t = Mathf.Clamp01(time * 20f);
			float inv = 1f - t;
			float scale = 1f - inv * inv * inv;
			spriteRenderer.transform.localScale = Vector3.one * scale;
			time += Time.deltaTime;
		}

		private void Hide()
		{
			time = 1f;
			if (spriteRenderer != null)
			{
				spriteRenderer.transform.localScale = Vector3.zero;
			}
		}

		public JudgmentView()
		{
		}
	}
}
