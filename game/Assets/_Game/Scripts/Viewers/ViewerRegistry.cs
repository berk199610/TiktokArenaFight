using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>All viewers seen this stream (real and bots), by userId. Costume is derived from the TikTok id (§7.3).</summary>
    public class ViewerRegistry
    {
        readonly Dictionary<string, Viewer> byId = new Dictionary<string, Viewer>();
        readonly List<Viewer> all = new List<Viewer>();
        readonly PersistenceService persistence;

        public IReadOnlyList<Viewer> All => all;

        public IEnumerable<ViewerRecord> BotRecords
        {
            get { foreach (var v in all) if (v.isBot && v.record != null) yield return v.record; }
        }

        public ViewerRegistry(PersistenceService persistence)
        {
            this.persistence = persistence;
        }

        public Viewer Find(string userId)
        {
            byId.TryGetValue(userId, out var v);
            return v;
        }

        public Viewer GetOrCreate(string userId, string nickname, string avatarUrl)
        {
            if (byId.TryGetValue(userId, out var v))
            {
                if (!string.IsNullOrEmpty(nickname)) v.nickname = nickname;
                if (v.record != null) persistence.GetOrCreate(userId, nickname, avatarUrl, v.costumeId);
                return v;
            }
            int costume = CostumeFor(userId);
            var rec = persistence.GetOrCreate(userId, nickname, avatarUrl, costume);
            if (rec.costumeId < 0 || rec.costumeId >= CostumeCatalog.Count) rec.costumeId = costume;
            v = new Viewer { userId = userId, nickname = nickname, record = rec, costumeId = rec.costumeId };
            byId[userId] = v;
            all.Add(v);
            return v;
        }

        public Viewer CreateBot(string name)
        {
            string id = "bot:" + name;
            if (byId.TryGetValue(id, out var existing)) return existing;
            int costume = CostumeFor(id);
            // bots get an in-memory record so they can appear on the scoreboard (never saved, doc §3)
            var v = new Viewer { userId = id, nickname = name, isBot = true, costumeId = costume, record = new ViewerRecord { userId = id, nickname = name, costumeId = costume } };
            byId[id] = v;
            all.Add(v);
            return v;
        }

        public static int CostumeFor(string userId)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in userId) { h ^= c; h *= 16777619; }
                return (int)(h % (uint)CostumeCatalog.Count);
            }
        }
    }
}
