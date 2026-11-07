// Connector 2.5 (protobuf v3) plus its documented legacy payloads.
// See https://ttlc.docs.isaackogan.com/guides/gifts/ and the installed legacy converter.
const integer = (value) => {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.min(2147483647, Math.floor(n)) : 0;
};
const firstPositive = (...values) => values.map(integer).find((n) => n > 0) || 0;
const firstText = (...values) => values.find((v) => v != null && String(v) !== '' && String(v) !== '0');
const imageUrl = (image) => image?.url?.[0] || image?.urlList?.[0] || '';

export function readUser(data) {
  const u = data.user || data;
  const uniqueId = firstText(u.uniqueId, u.displayId) || '';
  const numeric = firstText(u.userId, u.idStr, u.id);
  // Missing identity must not merge unrelated viewers into one "anon" fighter.
  const id = numeric || uniqueId;
  if (!id) return null;
  return {
    userId: 'tt:' + String(id),
    nickname: u.nickname || uniqueId || 'viewer',
    avatarUrl: u.profilePictureUrl || imageUrl(u.profilePicture) || imageUrl(u.avatarThumb) || imageUrl(u.avatarLarge),
  };
}

export class GiftEvents {
  constructor(now = Date.now) {
    this.now = now;
    this.streaks = new Map();
    this.messages = new Map();
  }

  reset() { this.streaks.clear(); this.messages.clear(); }

  normalize(d, roomId = '') {
    const u = readUser(d);
    if (!u) return null;
    const g = d.gift || {}, details = d.giftDetails || {}, extended = d.extendedGiftInfo || {};
    // A present but incomplete `gift` must not hide the fetched gift catalog.
    const coins = firstPositive(g.diamondCount, g.diamond_count, details.diamondCount, details.diamond_count,
      d.diamondCount, d.diamond_count, extended.diamondCount, extended.diamond_count);
    const id = firstText(d.giftId, g.id, g.gift_id, details.id, extended.id) || '';
    const giftName = firstText(g.name, g.giftName, details.name, details.giftName, d.giftName, extended.name) || String(id || 'gift');
    const giftType = firstPositive(g.type, g.giftType, g.gift_type, details.type, details.giftType, d.giftType, extended.type);
    const streakable = giftType === 1 || g.combo === true;
    const repeat = firstPositive(d.repeatCount, g.repeat_count, d.repeat_count) || 1;
    const ended = [true, 1, '1'].includes(d.repeatEnd ?? g.repeat_end ?? d.repeat_end);
    const now = this.now();
    const messageId = firstText(d.common?.msgId, d.msgId, d.messageId);
    const room = String(d.common?.roomId || roomId);
    if (messageId) {
      const key = `${room}:${messageId}:${repeat}:${ended}`;
      if (this.messages.has(key)) return null;
      this.messages.set(key, now);
    }

    let count = repeat;
    if (streakable) {
      const group = firstText(d.groupId, g.groupId) || '';
      const key = `${room}:${u.userId}:${id || giftName}:${group}`;
      let last = this.streaks.get(key);
      // Legacy payloads can omit groupId. The next growing sequence is a new streak.
      if (!group && last?.ended && (!ended || repeat < last.n || (messageId && String(messageId) !== last.messageId))) last = null;
      count = Math.max(0, repeat - (last?.n || 0));
      // Keep a completed group briefly: a replayed final frame must not pay twice.
      this.streaks.set(key, { n: Math.max(repeat, last?.n || 0), ended, messageId: String(messageId || ''), at: now });
    }
    if (count <= 0) return null;
    return { type: 'gift', ...u, coins, giftName: String(giftName), count };
  }

  clean() {
    const cutoff = this.now() - 10 * 60 * 1000;
    for (const [key, value] of this.streaks) if (value.at < cutoff) this.streaks.delete(key);
    for (const [key, at] of this.messages) if (at < cutoff) this.messages.delete(key);
  }
}
