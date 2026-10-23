using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// Per-project configuration: which tables, which languages, what the game is, what must not be
    /// translated. Lives in the game repo. The tool path is per machine (EditorPrefs), never here,
    /// and no API key exists anywhere in this package: llm-batch reads its own key store.
    /// </summary>
    [CreateAssetMenu(menuName = "BizSim/Localization LLM Settings", fileName = "BizSimLocalizationLlmSettings")]
    public class LlmTranslationSettings : ScriptableObject
    {
        public const string DefaultPath = "Assets/Editor/BizSimLocalizationLlmSettings.asset";

        [Serializable]
        public class FixedTerm
        {
            public string term;
            public string locale;
            public string translation;
        }

        [Serializable]
        public class LengthRule
        {
            [Tooltip("Keys starting with this prefix; empty = every key")]
            public string keyPrefix;
            [Tooltip("Hard limit in characters, 0 = none")]
            public int maxChars;
            [Tooltip("Limit as a multiple of the source length, 0 = none")]
            public float maxRatio;
        }

        public List<StringTableCollection> collections = new List<StringTableCollection>();
        public string sourceLocale = "en";
        [Tooltip("Empty = every locale of the collections except the source")]
        public List<string> targetLocales = new List<string>();
        [TextArea(3, 8)]
        public string context = "";
        public List<string> doNotTranslate = new List<string>();
        public List<FixedTerm> fixedTerms = new List<FixedTerm>();
        public List<LengthRule> lengthRules = new List<LengthRule>();
        public string model = "claude-haiku-5-5";
        public int chunk = 40;
        [Tooltip("Project-relative; keep it in the repo so changed-source detection survives")]
        public string stateFile = "docs/translations/llm-translation-state.json";
        [Tooltip("Project-relative folder for job, batch state and results. Not Temp/: Unity wipes it when " +
                 "the Editor closes, which would lose a batch between Send and Fetch")]
        public string workDir = "Library/LocalizationLlm";

        public static LlmTranslationSettings Load()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LlmTranslationSettings));
            return guids.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<LlmTranslationSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public int MaxLength(string key, string source)
        {
            int best = 0;
            foreach (var rule in lengthRules)
            {
                if (!string.IsNullOrEmpty(rule.keyPrefix) && !key.StartsWith(rule.keyPrefix, StringComparison.Ordinal))
                    continue;
                if (rule.maxChars > 0)
                    best = best == 0 ? rule.maxChars : Math.Min(best, rule.maxChars);
                if (rule.maxRatio > 0)
                {
                    int byRatio = Mathf.CeilToInt(source.Length * rule.maxRatio);
                    best = best == 0 ? byRatio : Math.Min(best, byRatio);
                }
            }
            return best;
        }
    }
}
