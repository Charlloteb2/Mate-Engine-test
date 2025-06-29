using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;

public class LanguageDropdownHandler : MonoBehaviour
{
    [Tooltip("Add all TMP_Dropdowns that should reflect the selected language")]
    [SerializeField] private List<TMP_Dropdown> languageDropdowns = new List<TMP_Dropdown>();

    private List<Locale> sortedLocales;
    private bool isInitializing = true;

    private void Start()
    {
        var allLocales = LocalizationSettings.AvailableLocales.Locales;

        // Ordena por nome legível
        sortedLocales = allLocales.OrderBy(locale => locale.LocaleName).ToList();

        // Cria lista de nomes amigáveis para o dropdown
        List<string> displayNames = sortedLocales.Select(l => l.LocaleName).ToList();

        // Aplica aos dropdowns
        foreach (var dropdown in languageDropdowns)
        {
            if (dropdown != null)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(displayNames);
                dropdown.onValueChanged.AddListener(OnLanguageChanged);
            }
        }

        // Define idioma salvo
        string savedCode = SaveLoadHandler.Instance.data.selectedLocaleCode;
        int savedIndex = sortedLocales.FindIndex(locale => locale.Identifier.Code == savedCode);
        if (savedIndex < 0) savedIndex = 0;

        foreach (var dropdown in languageDropdowns)
        {
            if (dropdown != null)
                dropdown.SetValueWithoutNotify(savedIndex);
        }

        LocalizationSettings.SelectedLocale = sortedLocales[savedIndex];
        isInitializing = false;
    }

    private void OnLanguageChanged(int index)
    {
        if (isInitializing) return;
        if (index < 0 || index >= sortedLocales.Count) return;

        var selected = sortedLocales[index];
        LocalizationSettings.SelectedLocale = selected;

        // Sincroniza todos os dropdowns
        foreach (var dropdown in languageDropdowns)
        {
            if (dropdown != null && dropdown.value != index)
            {
                dropdown.SetValueWithoutNotify(index);
            }
        }

        SaveLoadHandler.Instance.data.selectedLocaleCode = selected.Identifier.Code;
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
