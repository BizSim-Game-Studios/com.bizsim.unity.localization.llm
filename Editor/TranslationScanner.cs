using System.Collections.Generic;
using System.Linq;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>One string that needs a translation into one locale.</summary>
    public class WorkItem
    {
        public string Collection;
        public long KeyId;
        public string Key;
        public string Source;
        public string Locale;
        public bool Changed;
        public bool Smart;
        public string Comment;

        public string ItemId => Collection + "|" + Key;
    }

    /// <summary>
    /// Finds what to send: a target entry that is missing or empty, or one whose source text no
    /// longer matches the hash recorded when it was translated. A translation with no recorded hash
    /// (written by hand, or before this tool) counts as current; "Record baseline" stamps those.
    /// </summary>
    public static class TranslationScanner
    {
        public static List<WorkItem> Scan(LlmTranslationSettings settings, TranslationState state)
        {
            var work = new List<WorkItem>();
            foreach (var collection in settings.collections.Where(c => c != null))
            {
                var source = collection.GetTable(new LocaleIdentifier(settings.sourceLocale)) as StringTable;
                if (source == null)
                    continue;
                foreach (var target in TargetTables(settings, collection))
                {
                    string locale = target.LocaleIdentifier.Code;
                    foreach (var shared in collection.SharedData.Entries)
                    {
                        var src = source.GetEntry(shared.Id);
                        if (src == null || string.IsNullOrWhiteSpace(src.Value))
                            continue;
                        var dst = target.GetEntry(shared.Id);
                        bool missing = dst == null || string.IsNullOrWhiteSpace(dst.Value);
                        string recorded = state.SourceHashAt(collection.TableCollectionName, shared.Id, locale);
                        bool changed = !missing && recorded != null && recorded != TranslationState.Hash(src.Value);
                        if (!missing && !changed)
                            continue;
                        work.Add(new WorkItem
                        {
                            Collection = collection.TableCollectionName,
                            KeyId = shared.Id,
                            Key = shared.Key,
                            Source = src.Value,
                            Locale = locale,
                            Changed = changed,
                            Smart = src.IsSmart,
                            Comment = shared.Metadata.GetMetadata<Comment>()?.CommentText
                        });
                    }
                }
            }
            return work;
        }

        public static IEnumerable<StringTable> TargetTables(LlmTranslationSettings settings,
            StringTableCollection collection)
        {
            foreach (var table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (code == settings.sourceLocale)
                    continue;
                if (settings.targetLocales.Count > 0 && !settings.targetLocales.Contains(code))
                    continue;
                yield return table;
            }
        }

        /// <summary>Stamps every existing non-empty translation as current for its source text.</summary>
        public static int RecordBaseline(LlmTranslationSettings settings, TranslationState state)
        {
            int count = 0;
            foreach (var collection in settings.collections.Where(c => c != null))
            {
                var source = collection.GetTable(new LocaleIdentifier(settings.sourceLocale)) as StringTable;
                if (source == null)
                    continue;
                foreach (var target in TargetTables(settings, collection))
                    foreach (var shared in collection.SharedData.Entries)
                    {
                        var src = source.GetEntry(shared.Id);
                        var dst = target.GetEntry(shared.Id);
                        if (src == null || dst == null || string.IsNullOrWhiteSpace(dst.Value))
                            continue;
                        if (state.SourceHashAt(collection.TableCollectionName, shared.Id,
                                target.LocaleIdentifier.Code) != null)
                            continue;
                        state.Record(collection.TableCollectionName, shared.Id, shared.Key,
                            target.LocaleIdentifier.Code, src.Value);
                        count++;
                    }
            }
            return count;
        }
    }
}
