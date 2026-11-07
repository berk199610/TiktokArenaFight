using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum ViewerEventType { Comment, Like, Gift, Follow, Share, Join }

    /// <summary>One event from TikTok (or the simulator / DevKeys), DESIGN §10.2 schema.</summary>
    [Serializable]
    public class ViewerEvent
    {
        public string type;       // gift | like | comment | follow | share | join
        public string userId;
        public string nickname;
        public string avatarUrl;
        public int coins;         // coin value of ONE gift
        public string giftName;
        public int count = 1;     // likes in this batch, or gift repeat count (already de-duplicated by the bridge)
        public string text;
        public double ts;

        public ViewerEventType Kind
        {
            get
            {
                switch (type)
                {
                    case "gift": return ViewerEventType.Gift;
                    case "like": return ViewerEventType.Like;
                    case "comment": return ViewerEventType.Comment;
                    case "follow": return ViewerEventType.Follow;
                    case "share": return ViewerEventType.Share;
                    default: return ViewerEventType.Join;
                }
            }
        }
    }

    /// <summary>Persistent per-viewer record (DESIGN §7.5). Saved as JSON between streams.</summary>
    [Serializable]
    public class ViewerRecord
    {
        public string userId;
        public string nickname;
        public string avatarUrl;
        public int costumeId;
        public bool goldUnlocked;
        public long totalCoins;
        public long totalLikes;
        public long totalComments;
        public int wins;
        public int kills;
        public int deaths;
        public int points;
        public string firstSeen;
        public string lastSeen;
    }

    public enum ViewerState { Watching, Queued, InArena, Dead, WaitingReentry }

    /// <summary>
    /// Runtime state of a viewer during this stream (only in memory): last interaction, protection, queue, penalty.
    /// Bots are viewers too (isBot) but never get a record.
    /// </summary>
    public class Viewer
    {
        public string userId;
        public string nickname;
        public bool isBot;
        public ViewerRecord record;     // null for bots
        public int costumeId;
        public ViewerState state = ViewerState.Watching;
        public Fighter fighter;

        public float lastInteraction = -9999f;
        public float lastGiftTime = -9999f;
        public float deadUntil;
        public float diedAt;
        public bool interactedSinceDeath;
        public int roundCoins;          // coins sent this round (crown = most)
        public long streamCoins;
        public float queuedAt;
        public int roundKills;

        // like rate limiting: max N likes counted per second
        public float likeWindowStart;
        public int likesInWindow;

        // activity history for "who is least active in the last 2 minutes" (eviction / queue order)
        readonly Queue<float> interactions = new Queue<float>();

        // gift moves that must wait (dead in final battle -> next round start)
        public readonly List<PendingGift> pendingGifts = new List<PendingGift>();

        public bool IsGifter(float now, float protectionSec) => now - lastGiftTime <= protectionSec;
        public bool EverGiftedThisStream => streamCoins > 0;

        public void NoteInteraction(float now)
        {
            lastInteraction = now;
            interactions.Enqueue(now);
            while (interactions.Count > 64) interactions.Dequeue();
        }

        public int InteractionsSince(float since)
        {
            int n = 0;
            foreach (var t in interactions) if (t >= since) n++;
            return n;
        }

        public string DisplayName => string.IsNullOrEmpty(nickname) ? userId : nickname;
    }

    public struct PendingGift
    {
        public int coins;
        public int count;
        public string giftName;
    }
}
