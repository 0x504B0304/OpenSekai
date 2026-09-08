using TMPro;
using UnityEngine;

namespace Sekai.Localization
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(TMP_Text))]
	public sealed class LocalizedTextBinding : MonoBehaviour
	{
		[SerializeField] private string _key;
		private TMP_Text _text;

		public string Key
		{
			get => _key;
			set { _key = value; Refresh(); }
		}

		private void Awake()
		{
			_text = GetComponent<TMP_Text>();
		}

		private void OnEnable()
		{
			LocalizationManager.LanguageChanged += Refresh;
			Refresh();
		}

		private void OnDisable()
		{
			LocalizationManager.LanguageChanged -= Refresh;
		}

		public void Refresh()
		{
			if (_text == null) _text = GetComponent<TMP_Text>();
			if (_text != null && !string.IsNullOrEmpty(_key))
			{
				_text.textWrappingMode = TextWrappingModes.Normal;
				_text.overflowMode = TextOverflowModes.Ellipsis;
				_text.text = LocalizationManager.Get(_key);
			}
		}
	}
}
