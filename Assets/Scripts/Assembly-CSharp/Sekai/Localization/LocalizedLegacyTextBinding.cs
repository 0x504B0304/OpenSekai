using UnityEngine;
using UnityEngine.UI;

namespace Sekai.Localization
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Text))]
	public sealed class LocalizedLegacyTextBinding : MonoBehaviour
	{
		[SerializeField] private string _key;
		private Text _text;

		public string Key
		{
			get => _key;
			set
			{
				_key = value;
				Refresh();
			}
		}

		private void Awake()
		{
			_text = GetComponent<Text>();
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
			if (_text == null) _text = GetComponent<Text>();
			if (_text != null && !string.IsNullOrEmpty(_key)) _text.text = LocalizationManager.Get(_key);
		}
	}
}
