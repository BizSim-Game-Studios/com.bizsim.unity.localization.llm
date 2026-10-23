using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// Scan, estimate, send, fetch, review the diff, apply. Nothing reaches a table before Apply,
    /// and Apply writes only the rows that passed llm-batch's checks and are ticked here.
    /// </summary>
    public class LlmTranslationWindow : EditorWindow
    {
        LlmTranslationSettings _settings;
        string _log = "";
        List<LlmTranslation.Row> _rows = new List<LlmTranslation.Row>();
        Vector2 _scroll;
        bool _failedOnly;

        [MenuItem("Tools/BizSim/Localization LLM")]
        static void Open() => GetWindow<LlmTranslationWindow>("Localization LLM");

        void OnEnable()
        {
            _settings = LlmTranslationSettings.Load();
            _rows = _settings == null ? new List<LlmTranslation.Row>() : LlmTranslation.LoadRows();
        }

        void OnGUI()
        {
            _settings = (LlmTranslationSettings)EditorGUILayout.ObjectField("Settings", _settings,
                typeof(LlmTranslationSettings), false);
            if (_settings == null)
            {
                if (GUILayout.Button("Create settings at " + LlmTranslationSettings.DefaultPath))
                    CreateSettings();
                return;
            }
            LlmBatchRunner.ToolPath = EditorGUILayout.TextField("llm-batch tool", LlmBatchRunner.ToolPath);
            LlmBatchRunner.Python = EditorGUILayout.TextField("Python", LlmBatchRunner.Python);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan")) _log = Safe(LlmTranslation.Scan);
            if (GUILayout.Button("Dry run (estimate)")) _log = Safe(() => LlmTranslation.Submit(false));
            if (GUILayout.Button("Send") && EditorUtility.DisplayDialog("Send batch",
                    "Submit the batch? This costs money (see the dry-run estimate).", "Send", "Cancel"))
                _log = Safe(() => LlmTranslation.Submit(true));
            if (GUILayout.Button("Status")) _log = Safe(LlmTranslation.Status);
            if (GUILayout.Button("Fetch")) _log = Safe(() => { _rows = LlmTranslation.Fetch(); return Summary(); });
            if (GUILayout.Button("Record baseline")) _log = Safe(LlmTranslation.RecordBaseline);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_log) ? "Scan to start." : _log, MessageType.None);
            DrawRows();

            using (new EditorGUI.DisabledScope(!_rows.Any(r => r.Accept && r.Result.ok)))
                if (GUILayout.Button($"Apply {_rows.Count(r => r.Accept && r.Result.ok)} ticked rows"))
                    _log = Safe(() => $"{LlmTranslation.Apply(_rows)} written; export the CSV if the project keeps one");
        }

        void DrawRows()
        {
            if (_rows.Count == 0)
                return;
            _failedOnly = EditorGUILayout.ToggleLeft("Show failed checks only", _failedOnly);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _rows.Where(r => !_failedOnly || !r.Result.ok))
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!row.Result.ok))
                    row.Accept = EditorGUILayout.Toggle(row.Accept, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{row.Result.locale}  {row.Key}", GUILayout.Width(260));
                EditorGUILayout.LabelField(string.IsNullOrEmpty(row.OldText) ? "(empty)" : row.OldText,
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(row.Result.text, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
                foreach (var e in row.Result.errors)
                    EditorGUILayout.LabelField("    error: " + e, EditorStyles.miniBoldLabel);
                foreach (var w in row.Result.warnings)
                    EditorGUILayout.LabelField("    warning: " + w, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        string Summary() =>
            $"{_rows.Count} rows: {_rows.Count(r => r.Result.ok)} passed, {_rows.Count(r => !r.Result.ok)} failed, " +
            $"{_rows.Count(r => r.Result.warnings.Count > 0)} with warnings";

        void CreateSettings()
        {
            _settings = CreateInstance<LlmTranslationSettings>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LlmTranslationSettings.DefaultPath));
            AssetDatabase.CreateAsset(_settings, LlmTranslationSettings.DefaultPath);
            AssetDatabase.SaveAssets();
        }

        static string Safe(System.Func<string> action)
        {
            try { return action(); }
            catch (System.Exception e) { return "Error: " + e.Message; }
        }
    }
}
