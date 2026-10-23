using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// The whole flow as static calls, for the window and for CLI / MCP runs:
    /// Scan -> Submit(send: false) for the estimate -> Submit(send: true) -> Status -> Fetch -> Apply.
    /// </summary>
    public static class LlmTranslation
    {
        public class Row
        {
            public TranslationJob.Result Result;
            public string Collection;
            public string Key;
            public string OldText;
            public bool Accept;
        }

        static LlmTranslationSettings Settings()
        {
            var s = LlmTranslationSettings.Load();
            if (s == null)
                throw new System.InvalidOperationException(
                    "No LlmTranslationSettings asset; create one at " + LlmTranslationSettings.DefaultPath);
            return s;
        }

        static string Work(LlmTranslationSettings s, string file) => Path.Combine(s.workDir, file).Replace('\\', '/');

        public static string Scan()
        {
            var s = Settings();
            var work = TranslationScanner.Scan(s, TranslationState.Load(s.stateFile));
            if (work.Count == 0)
                return "nothing to translate";
            var perLocale = work.GroupBy(w => w.Locale).OrderBy(g => g.Key)
                .Select(g => $"{g.Key} {g.Count(w => !w.Changed)}+{g.Count(w => w.Changed)}");
            return $"{work.Count} strings ({work.Count(w => !w.Changed)} missing, {work.Count(w => w.Changed)} changed) " +
                   $"over {work.Select(w => w.ItemId).Distinct().Count()} keys; per locale missing+changed: " +
                   string.Join(", ", perLocale);
        }

        public static string Submit(bool send)
        {
            var s = Settings();
            var work = TranslationScanner.Scan(s, TranslationState.Load(s.stateFile));
            if (work.Count == 0)
                return "nothing to translate";
            Directory.CreateDirectory(s.workDir);
            File.WriteAllText(Work(s, "job.json"), TranslationJob.Build(s, work), new UTF8Encoding(false));
            var args = new List<string>
            {
                "translate-submit", Work(s, "job.json"), "--state", Work(s, "state.json"),
                "--model", s.model, "--chunk", s.chunk.ToString()
            };
            if (send)
                args.Add("--send");
            return LlmBatchRunner.Run(args.ToArray()).output;
        }

        public static string Status() => LlmBatchRunner.Run("status", Work(Settings(), "state.json")).output;

        public static List<Row> Fetch()
        {
            var s = Settings();
            var (code, output) = LlmBatchRunner.Run("translate-fetch", Work(s, "state.json"), "--out",
                Work(s, "results.json"));
            if (code != 0)
                throw new System.InvalidOperationException(output);
            return LoadRows();
        }

        public static List<Row> LoadRows()
        {
            var s = Settings();
            var path = Work(s, "results.json");
            if (!File.Exists(path))
                return new List<Row>();
            var rows = new List<Row>();
            foreach (var r in TranslationJob.ReadResults(File.ReadAllText(path, Encoding.UTF8)))
            {
                int bar = r.id.IndexOf('|');
                var row = new Row { Result = r, Collection = r.id.Substring(0, bar), Key = r.id.Substring(bar + 1) };
                var table = Table(row.Collection, r.locale);
                var shared = table?.SharedData.GetEntry(row.Key);
                row.OldText = shared == null ? null : table.GetEntry(shared.Id)?.Value;
                row.Accept = r.ok;
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Writes the accepted rows that passed every check; returns how many were written.</summary>
        public static int Apply(IEnumerable<Row> rows)
        {
            var s = Settings();
            var state = TranslationState.Load(s.stateFile);
            int written = 0;
            foreach (var row in rows.Where(r => r.Accept && r.Result.ok))
            {
                var table = Table(row.Collection, row.Result.locale);
                var collection = LocalizationEditorSettings.GetStringTableCollection(row.Collection);
                var source = collection?.GetTable(new LocaleIdentifier(s.sourceLocale)) as StringTable;
                var shared = table?.SharedData.GetEntry(row.Key);
                if (table == null || source == null || shared == null)
                    continue;
                Undo.RecordObject(table, "LLM translation");
                var entry = table.GetEntry(shared.Id);
                if (entry == null)
                    table.AddEntry(shared.Id, row.Result.text);
                else
                    entry.Value = row.Result.text;
                if (source.GetEntry(shared.Id)?.IsSmart == true)
                    table.GetEntry(shared.Id).IsSmart = true;
                EditorUtility.SetDirty(table);
                state.Record(row.Collection, shared.Id, row.Key, row.Result.locale, source.GetEntry(shared.Id)?.Value);
                written++;
            }
            AssetDatabase.SaveAssets();
            state.Save(s.stateFile);
            return written;
        }

        /// <summary>CLI form of Apply: every row that passed its checks.</summary>
        public static string ApplyAllPassing()
        {
            var rows = LoadRows();
            int written = Apply(rows);
            return $"{written} written, {rows.Count(r => !r.Result.ok)} failed checks (not written)";
        }

        public static string RecordBaseline()
        {
            var s = Settings();
            var state = TranslationState.Load(s.stateFile);
            int n = TranslationScanner.RecordBaseline(s, state);
            state.Save(s.stateFile);
            return $"{n} existing translations recorded as current";
        }

        static StringTable Table(string collectionName, string locale) =>
            LocalizationEditorSettings.GetStringTableCollection(collectionName)
                ?.GetTable(new LocaleIdentifier(locale)) as StringTable;
    }
}
