using Sekai.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    [DisallowMultipleComponent]
    public sealed class MenuTypographyBinding : MonoBehaviour
    {
        [SerializeField] private MenuTextRole role;
        public MenuTextRole Role { get => role; set => role = value; }
        private TMP_Text tmp;
        private Text legacy;
        private void Awake() { tmp = GetComponent<TMP_Text>(); legacy = GetComponent<Text>(); }
        private void OnEnable() { LocalizationManager.LanguageChanged += Refresh; Refresh(); }
        private void OnDisable() => LocalizationManager.LanguageChanged -= Refresh;
        public void Refresh()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            if (legacy == null) legacy = GetComponent<Text>();
            var font = MenuTypography.Get(role);
            if (tmp != null)
            {
                if (tmp.font != font) tmp.font = font;
                if (tmp.fontSharedMaterial != font.material) tmp.fontSharedMaterial = font.material;
                tmp.fontStyle &= ~FontStyles.Bold;
                tmp.fontWeight = FontWeight.Regular;
            }
            if (legacy != null)
            {
                // Legacy Text has no TMP fallback chain: SC also covers Latin strings.
                string language = LocalizationManager.CurrentLanguage == LocalizationManager.English ? LocalizationManager.SimplifiedChinese : LocalizationManager.CurrentLanguage;
                legacy.font = Resources.Load<Font>(MenuTypography.SourcePath(role, language));
                foreach (char character in legacy.text)
                    if (!char.IsControl(character) && !legacy.font.HasCharacter(character))
                    { legacy.font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular"); break; }
                legacy.fontStyle = FontStyle.Normal;
            }
        }
    }
}
