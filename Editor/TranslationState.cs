using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// What the source text was when each translation was last written, so a later edit of the
    /// source marks that translation as changed. One row per key and source hash, listing the
    /// locales translated from that source (a key whose locales were all done together is one row,
    /// which keeps the file small: about 0.5 MB for 1900 keys x 31 locales, measured on Junkyard Tycoon). Keyed by collection +
    /// key id, so a renamed key keeps its history. Stored as a sidecar JSON in the repo; the table
    /// assets are never touched for bookkeeping.
    /// </summary>
    [Serializable]
    public class TranslationState
    {
        [Serializable]
        public class Row
        {
            public string collection;
            public long keyId;
            public string key;
            public string sourceHash;
            [Tooltip("Comma-separated, sorted: one field keeps the file small and its diffs readable")]
            public string locales = "";

            [NonSerialized] List<string> _list;

            public List<string> List => _list ??= new List<string>(
                locales.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            public void Flush()
            {
                List.Sort(StringComparer.Ordinal);
                locales = string.Join(",", List);
            }
        }

        public List<Row> rows = new List<Row>();

        [NonSerialized] Dictionary<string, List<Row>> _byKey;

        static string KeySlot(string collection, long keyId) => collection + "|" + keyId;

        public static string Hash(string text)
        {
            using (var sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "")
                    .Substring(0, 16);
        }

        public static TranslationState Load(string path)
        {
            var state = File.Exists(path)
                ? JsonUtility.FromJson<TranslationState>(File.ReadAllText(path, Encoding.UTF8)) ?? new TranslationState()
                : new TranslationState();
            state.Reindex();
            return state;
        }

        public void Save(string path)
        {
            foreach (var r in rows)
                r.Flush();
            rows.RemoveAll(r => r.locales.Length == 0);
            rows.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.collection, b.collection);
                if (c == 0) c = a.keyId.CompareTo(b.keyId);
                return c != 0 ? c : string.CompareOrdinal(a.sourceHash, b.sourceHash);
            });
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(this, true), new UTF8Encoding(false));
        }

        public string SourceHashAt(string collection, long keyId, string locale) =>
            _byKey.TryGetValue(KeySlot(collection, keyId), out var list)
                ? list.FirstOrDefault(r => r.List.Contains(locale))?.sourceHash
                : null;

        public void Record(string collection, long keyId, string key, string locale, string sourceText)
        {
            string hash = Hash(sourceText);
            var slot = KeySlot(collection, keyId);
            if (!_byKey.TryGetValue(slot, out var list))
                _byKey[slot] = list = new List<Row>();
            foreach (var other in list)
                other.List.Remove(locale);
            var row = list.FirstOrDefault(r => r.sourceHash == hash);
            if (row == null)
            {
                row = new Row { collection = collection, keyId = keyId, sourceHash = hash };
                list.Add(row);
                rows.Add(row);
            }
            row.key = key;
            row.List.Add(locale);
        }

        void Reindex()
        {
            _byKey = new Dictionary<string, List<Row>>();
            foreach (var row in rows)
            {
                var slot = KeySlot(row.collection, row.keyId);
                if (!_byKey.TryGetValue(slot, out var list))
                    _byKey[slot] = list = new List<Row>();
                list.Add(row);
            }
        }
    }
}
