// TranslateText.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMPro.TextMeshProUGUI))]

// Place on all t:TextMeshProUGUI or t:Text
public class TranslateText : MonoBehaviour
{
    bool init = false;
    string englishText;
    int lastLanguageUpdated = -1;
    TMPro.TextMeshProUGUI txtDisplay;

    void Start()
    {
        Init();
        UpdateTextDisplay();
    }

    private void OnEnable()
    {
        if(init)
            UpdateTextDisplay();
    }

    public void SetEnglishText(string newText)
    {
        Init();

        englishText = newText;
        txtDisplay.text = englishText;
        lastLanguageUpdated = -1;
        
        UpdateTextDisplay();
    }

    void Init()
    {
        if (init)
            return;

        txtDisplay = GetComponent<TMPro.TextMeshProUGUI>();
        UnityEngine.Assertions.Assert.IsNotNull(txtDisplay);
        englishText = txtDisplay.text;
        lastLanguageUpdated = -1;
        init = true;

        ServiceLocator.instance.GetService<Translator>().DisplayLanguageChanged += UpdateTextDisplay;
    }

    internal void ManuallySetTextToLoading(int displayLanguage)
    {
        Init();
        string[] loadingTranslations = {"Loading...", "Chargement...", "Caricamento in corso...", "Wird geladen...",
        "Cargando...",  "载入中...",   "Loading...",   "Carregando..."};
        int langIndex = displayLanguage + 1;
        UnityEngine.Assertions.Assert.IsTrue(langIndex >= 0 && langIndex < loadingTranslations.Length);
        txtDisplay.text = loadingTranslations[langIndex];
    }

    void UpdateTextDisplay()
    {
        if (this == null)
            return;

        Init();
        Translator translator = ServiceLocator.instance.GetService<Translator>();
        int displayLanguage = translator.GetUserDisplayLanguageIndex();

        if (displayLanguage == lastLanguageUpdated)
            return;

        lastLanguageUpdated = displayLanguage;
        if (translator.IsDisplayingEnglish())
        {
            txtDisplay.text = englishText;
        }
        else
        {
            string translatedText = translator.Translate(englishText.Trim(), displayLanguage);
            UnityEngine.Assertions.Assert.IsNotNull(translatedText);
            UnityEngine.Assertions.Assert.IsNotNull(txtDisplay, gameObject.name);
            txtDisplay.text = translatedText;
        }
    }
}
