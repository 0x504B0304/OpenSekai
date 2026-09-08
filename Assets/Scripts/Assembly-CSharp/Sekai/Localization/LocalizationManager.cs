using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace Sekai.Localization
{
	public static class LocalizationManager
	{
		public const string SimplifiedChinese = "zh-Hans";
		public const string Japanese = "ja";
		public const string English = "en";
		private const string PreferenceKey = "OpenSekai.Language";
		private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();
		private static string _currentLanguage;

		public static event Action LanguageChanged;

		public static string CurrentLanguage
		{
			get
			{
				if (string.IsNullOrEmpty(_currentLanguage)) SetLanguage(ResolveInitialLanguage(), false);
				return _currentLanguage;
			}
		}

		public static void SetLanguage(string language) => SetLanguage(language, true);

		public static string Get(string key)
		{
			if (string.IsNullOrEmpty(key)) return string.Empty;
			Dictionary<string, string> table = LoadTable(CurrentLanguage);
			if (table.TryGetValue(key, out string value)) return value;
			Dictionary<string, string> fallback = LoadTable(English);
			return fallback.TryGetValue(key, out value) ? value : key;
		}

		public static string Format(string key, params object[] args)
		{
			CultureInfo culture = CurrentLanguage == Japanese ? CultureInfo.GetCultureInfo("ja-JP") : CurrentLanguage == SimplifiedChinese ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US");
			return string.Format(culture, Get(key), args ?? Array.Empty<object>());
		}

		public static IReadOnlyDictionary<string, string> GetTable(string language) => LoadTable(NormalizeLanguage(language));

		private static void SetLanguage(string language, bool persist)
		{
			language = NormalizeLanguage(language);
			if (_currentLanguage == language) return;
			_currentLanguage = language;
			LoadTable(language);
			if (persist)
			{
				PlayerPrefs.SetString(PreferenceKey, language);
				PlayerPrefs.Save();
			}
			LanguageChanged?.Invoke();
		}

		private static string ResolveInitialLanguage()
		{
			if (PlayerPrefs.HasKey(PreferenceKey)) return PlayerPrefs.GetString(PreferenceKey);
			return Application.systemLanguage switch
			{
				SystemLanguage.ChineseSimplified => SimplifiedChinese,
				SystemLanguage.Chinese => SimplifiedChinese,
				SystemLanguage.Japanese => Japanese,
				_ => English
			};
		}

		private static string NormalizeLanguage(string language)
		{
			if (string.Equals(language, SimplifiedChinese, StringComparison.OrdinalIgnoreCase) || string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase)) return SimplifiedChinese;
			if (string.Equals(language, Japanese, StringComparison.OrdinalIgnoreCase) || language?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true) return Japanese;
			return English;
		}

		private static Dictionary<string, string> LoadTable(string language)
		{
			if (Tables.TryGetValue(language, out Dictionary<string, string> table)) return table;
			TextAsset asset = Resources.Load<TextAsset>("Localization/" + language);
			table = asset == null ? new Dictionary<string, string>() : JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text) ?? new Dictionary<string, string>();
			Tables[language] = table;
			return table;
		}
	}
}
