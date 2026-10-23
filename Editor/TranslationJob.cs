using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// Writes llm-batch's translate JOB.json from the scanned work and reads its RESULTS.json back.
    /// One item per key; its "targets" lists only the locales that key is missing or stale in.
    /// </summary>
    public static class TranslationJob
    {
        [Serializable]
        public class Result
        {
            public string id;
            public string locale;
            public string text;
            public bool ok;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        [Serializable]
        class ResultList
        {
            public List<Result> rows = new List<Result>();
        }

        public static string Build(LlmTranslationSettings settings, List<WorkItem> work)
        {
            var sb = new StringBuilder();
            sb.Append("{\"source_locale\":").Append(Q(settings.sourceLocale));
            var targets = work.Select(w => w.Locale).Distinct().OrderBy(l => l, StringComparer.Ordinal);
            sb.Append(",\"targets\":[").Append(string.Join(",", targets.Select(Q))).Append(']');
            sb.Append(",\"context\":").Append(Q(settings.context));
            sb.Append(",\"glossary\":{\"dnt\":[")
                .Append(string.Join(",", settings.doNotTranslate.Where(t => !string.IsNullOrEmpty(t)).Select(Q)))
                .Append("],\"fixed\":{");
            sb.Append(string.Join(",", settings.fixedTerms
                .Where(f => !string.IsNullOrEmpty(f.term) && !string.IsNullOrEmpty(f.locale))
                .GroupBy(f => f.term)
                .Select(g => Q(g.Key) + ":{" + string.Join(",", g.Select(f => Q(f.locale) + ":" + Q(f.translation))) + "}")));
            sb.Append("}},\"items\":[");
            bool first = true;
            foreach (var group in work.GroupBy(w => w.ItemId))
            {
                var w = group.First();
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(Q(w.ItemId)).Append(",\"text\":").Append(Q(w.Source));
                int max = settings.MaxLength(w.Key, w.Source);
                if (max > 0)
                    sb.Append(",\"max_len\":").Append(max);
                string note = Note(w);
                if (note.Length > 0)
                    sb.Append(",\"note\":").Append(Q(note));
                sb.Append(",\"targets\":[").Append(string.Join(",", group.Select(x => Q(x.Locale)))).Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static List<Result> ReadResults(string json) =>
            JsonUtility.FromJson<ResultList>("{\"rows\":" + json + "}").rows;

        static string Note(WorkItem w)
        {
            var parts = new List<string> { "key " + w.Key };
            if (!string.IsNullOrEmpty(w.Comment))
                parts.Add(w.Comment);
            if (w.Smart)
                parts.Add("Smart String: keep the {...} formatter syntax");
            return string.Join("; ", parts);
        }

        static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
