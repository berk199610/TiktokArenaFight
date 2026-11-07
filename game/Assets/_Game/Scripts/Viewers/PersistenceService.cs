using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// <summary>Viewer records in a JSON file next to the game data (DESIGN §7.5). Survives between streams.</summary>
    public class PersistenceService
    {
        [Serializable] class FileModel { public List<ViewerRecord> records = new List<ViewerRecord>(); }

        readonly Dictionary<string, ViewerRecord> records = new Dictionary<string, ViewerRecord>();
        public string FilePath { get; }
        bool dirty;
        float nextSave;

        public IEnumerable<ViewerRecord> All => records.Values;
        public int Count => records.Count;

        public PersistenceService()
        {
            string dataPath = Application.persistentDataPath;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-test-data") { dataPath = System.IO.Path.GetFullPath(args[i + 1]); Directory.CreateDirectory(dataPath); break; }
            FilePath = System.IO.Path.Combine(dataPath, "arena_records.json");
            Load();
        }

        void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var m = JsonUtility.FromJson<FileModel>(File.ReadAllText(FilePath));
                if (m?.records == null) return;
                foreach (var r in m.records)
                    if (!string.IsNullOrEmpty(r.userId)) records[r.userId] = r;
                Debug.Log($"[Arena] {records.Count} izleyici kaydı yüklendi: {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError("[Arena] Kayıt dosyası okunamadı: " + e.Message);
            }
        }

        public ViewerRecord Get(string userId)
        {
            records.TryGetValue(userId, out var r);
            return r;
        }

        public ViewerRecord GetOrCreate(string userId, string nickname, string avatarUrl, int costumeId)
        {
            if (!records.TryGetValue(userId, out var r))
            {
                r = new ViewerRecord
                {
                    userId = userId,
                    nickname = nickname,
                    avatarUrl = avatarUrl,
                    costumeId = costumeId,
                    firstSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                };
                records[userId] = r;
            }
            if (!string.IsNullOrEmpty(nickname)) r.nickname = nickname;
            if (!string.IsNullOrEmpty(avatarUrl)) r.avatarUrl = avatarUrl;
            r.lastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            dirty = true;
            return r;
        }

        public void MarkDirty() => dirty = true;

        public void Tick()
        {
            if (!dirty || Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 30f;
            Save();
        }

        public void Save()
        {
            try
            {
                var m = new FileModel();
                m.records.AddRange(records.Values);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(m));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
                dirty = false;
            }
            catch (Exception e)
            {
                Debug.LogError("[Arena] Kayıt yazılamadı: " + e.Message);
            }
        }

        /// <summary>"Skor tablosunu sıfırla": points, wins, kills, deaths cleared; costumes stay.</summary>
        public void ResetScores()
        {
            foreach (var r in records.Values) { r.points = 0; r.wins = 0; r.kills = 0; r.deaths = 0; }
            dirty = true;
            Save();
        }

        /// <summary>"Tüm kayıtları sil".</summary>
        public void DeleteAll()
        {
            records.Clear();
            dirty = true;
            Save();
        }
    }
}
