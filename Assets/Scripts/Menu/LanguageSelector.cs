using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Project.Menu
{
    public class LanguageSelector : MonoBehaviour
    {
        private const string PrefsKey = "SelectedLanguage";

        private void Awake()
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return;

            string code = PlayerPrefs.GetString(PrefsKey);
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null)
                LocalizationSettings.SelectedLocale = locale;
        }

        public void SetEnglish() => SetLocale("en");

        public void SetSpanish() => SetLocale("es");

        private void SetLocale(string code)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null)
            {
                Debug.LogWarning($"[LanguageSelector] Locale '{code}' no encontrado en AvailableLocales.");
                return;
            }

            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PrefsKey, code);
            PlayerPrefs.Save();
        }
    }
}
