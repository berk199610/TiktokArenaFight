// TikTok LIVE sağlayıcısı (DESIGN §10.2) — tiktok-live-connector 2.x.
//
//  * Her TikTok olayı simülatörle AYNI şemaya çevrilir:
//      { type: 'gift'|'like'|'comment'|'follow'|'share'|'join', userId, nickname, avatarUrl, coins, giftName, count, text, ts }
//    coins = TEK hediyenin coin (elmas) değeri, count = bu olayda yeni gelen adet.
//  * Hediye serileri (combo): TikTok seri boyunca artan repeatCount ile tekrar tekrar olay yollar, sonunda bir kez de
//    repeatEnd ile. Her olayda sadece YENİ gelen kısmı (fark) gönderiyoruz: hediye anında oyuna yansır ve hiçbiri iki
//    kez sayılmaz.
//  * Alan adları kütüphane sürümüne göre değişebiliyor (README örnekleri eski şemayı, kurulu 2.5 sürümü proto v3'ü
//    kullanıyor); ikisi de okunuyor. Her olay türünün ilk örneği bridge/data/ klasörüne yazılır (hata ayıklama için),
//    görülen hediyeler de data/gifts-seen.json'a (hangi hediye kaç coin).
//  * Bağlantı koparsa artan aralıklarla (10 s -> 60 s) yeniden bağlanır.
import fs from 'node:fs';
import path from 'node:path';
import { TikTokLiveConnection, WebcastEvent, ControlEvent } from 'tiktok-live-connector';
import { GiftEvents, readUser } from './tiktok-events.js';

const safeJson = (o) => JSON.stringify(o, (k, v) => (typeof v === 'bigint' ? v.toString() : v), 2);

export class TikTokProvider {
  constructor(emit, log, dataDir, createConnection = (username, options) => new TikTokLiveConnection(username, options)) {
    this.emit = emit;
    this.log = log;
    this.dataDir = dataDir;
    this.createConnection = createConnection;
    this.connected = false;
    this.status = 'kapalı';
    this.username = '';
    this.apiKey = '';
    this.conn = null;
    this.wanted = false;
    this.retries = 0;
    this.retryTimer = null;
    this.generation = 0;
    this.giftEvents = new GiftEvents();
    this.giftMetadata = new Map();
    this.lastEvent = null;
    this.sampled = new Set();
    this.giftsSeen = {};
    this.counts = { comment: 0, like: 0, gift: 0, follow: 0, share: 0, join: 0 };
    try { fs.mkdirSync(dataDir, { recursive: true }); } catch {}
    try { this.giftsSeen = JSON.parse(fs.readFileSync(path.join(dataDir, 'gifts-seen.json'), 'utf8')); } catch {}
    this.cleanupTimer = setInterval(() => this.giftEvents.clean(), 30000);
    this.cleanupTimer.unref();
  }

  info() {
    return { connected: this.connected, status: this.status, username: this.username, counts: this.counts, lastEvent: this.lastEvent };
  }

  async start(username, apiKey) {
    const previousUsername = this.username;
    const stopping = this.stop();
    const generation = this.generation;
    await stopping;
    if (generation !== this.generation) return;
    this.username = String(username || '').replace('https://www.tiktok.com/', '').replace('/live', '').replace('@', '').trim();
    this.apiKey = apiKey || '';
    if (previousUsername !== this.username) {
      this.giftEvents.reset();
      this.giftMetadata.clear();
    }
    if (!this.username) { this.status = 'kullanıcı adı girilmedi'; return; }
    this.wanted = true;
    this.retries = 0;
    this.connect();
  }

  async stop() {
    this.generation++;
    this.wanted = false;
    clearTimeout(this.retryTimer);
    const c = this.conn;
    this.conn = null;
    this.connected = false;
    this.status = 'kapalı';
    if (c) { try { await c.disconnect(); } catch {} }
  }

  sample(type, data) {
    if (this.sampled.has(type)) return;
    this.sampled.add(type);
    try { fs.writeFileSync(path.join(this.dataDir, `tiktok-ornek-${type}.json`), safeJson(data)); } catch {}
  }

  send(e) {
    if (!e.userId) return;
    this.counts[e.type] = (this.counts[e.type] || 0) + 1;
    this.lastEvent = { type: e.type, nickname: e.nickname, coins: e.coins || 0, count: e.count || 1, at: Date.now() };
    this.emit({ coins: 0, giftName: '', count: 1, text: '', ts: Date.now(), ...e });
  }

  connect() {
    if (!this.wanted) return;
    // Fetch optional gift metadata after the socket opens; a catalog failure must not stop LIVE events.
    const options = { processInitialData: false, enableExtendedGiftInfo: false };
    if (this.apiKey) options.signApiKey = this.apiKey;
    let conn;
    try { conn = this.createConnection(this.username, options); }
    catch (e) { this.status = 'hata: ' + e.message; this.log('TikTok bağlantısı kurulamadı:', e.message); return this.retry(); }
    this.conn = conn;
    this.status = 'bağlanıyor…';
    this.log(`TikTok'a bağlanılıyor: @${this.username}`);
    const listen = (event, handler) => conn.on(event, (data) => {
      if (this.conn === conn && this.wanted) handler(data);
    });

    listen(WebcastEvent.CHAT, (d) => {
      this.sample('chat', d);
      const u = readUser(d);
      if (u) this.send({ type: 'comment', ...u, text: d.content || d.comment || '' });
    });
    listen(WebcastEvent.LIKE, (d) => {
      this.sample('like', d);
      const u = readUser(d);
      const n = Number(d.count ?? d.likeCount ?? 1) || 1;
      if (u) this.send({ type: 'like', ...u, count: n });
    });
    listen(WebcastEvent.GIFT, (d) => {
      this.sample('gift', d);
      const metadata = d.extendedGiftInfo || this.giftMetadata.get(String(d.giftId));
      const event = this.giftEvents.normalize({ ...d, extendedGiftInfo: metadata }, conn.roomId);
      if (!event) return;
      const { coins, giftName: name } = event;
      if (coins <= 0) this.log('TikTok hediye değeri eksik; oyun kataloğu kullanılacak:', name);
      if (coins > 0 && this.giftsSeen[name] !== coins) {
        this.giftsSeen[name] = coins;
        try { fs.writeFileSync(path.join(this.dataDir, 'gifts-seen.json'), safeJson(this.giftsSeen)); } catch {}
      }
      this.send(event);
    });
    listen(WebcastEvent.FOLLOW, (d) => { this.sample('follow', d); const u = readUser(d); if (u) this.send({ type: 'follow', ...u }); });
    listen(WebcastEvent.SHARE, (d) => { this.sample('share', d); const u = readUser(d); if (u) this.send({ type: 'share', ...u }); });
    listen(WebcastEvent.MEMBER, (d) => { const u = readUser(d); if (u) this.send({ type: 'join', ...u }); });
    listen(WebcastEvent.STREAM_END, () => {
      this.connected = false;
      this.status = 'yayın bitti';
      this.log('TikTok yayını bitti.');
      this.retry(60);
    });
    listen(ControlEvent.DISCONNECTED, ({ code, reason } = {}) => {
      if (!this.connected) return;
      this.connected = false;
      this.status = 'bağlantı koptu, yeniden deneniyor';
      this.log('TikTok bağlantısı koptu', code || '', reason || '');
      this.retry();
    });
    // Keep an error listener even on retired connections: EventEmitter treats unhandled errors as fatal.
    conn.on(ControlEvent.ERROR, (e) => { if (this.conn === conn && this.wanted) this.log('TikTok hata:', e && (e.info || e.exception?.message || e)); });

    conn.connect().then((state) => {
      if (this.conn !== conn || !this.wanted) { conn.disconnect().catch(() => {}); return; }
      this.connected = true;
      this.retries = 0;
      this.status = `bağlı (@${this.username})`;
      this.log(`TikTok LIVE bağlandı: @${this.username} oda ${state.roomId}`);
      conn.fetchAvailableGifts().then((gifts) => {
        if (this.conn !== conn || !this.wanted || !Array.isArray(gifts)) return;
        this.giftMetadata = new Map(gifts.map((g) => [String(g.id), g]));
      }).catch(() => {
        if (this.conn === conn && this.wanted) this.log('TikTok hediye kataloğu alınamadı; olaylardaki hediye değerleri kullanılacak.');
      });
    }).catch((err) => {
      if (this.conn !== conn || !this.wanted) return;
      this.connected = false;
      const n = err && (err.name || err.constructor?.name) || '';
      if (n.includes('UserOffline')) this.status = `@${this.username} şu an canlı yayında değil (yayını başlatınca kendiliğinden bağlanır)`;
      else if (n.includes('RateLimit')) this.status = 'imza sunucusu sınırı doldu, biraz sonra tekrar denenecek';
      else if (/room id/i.test(String(err && err.message))) this.status = `@${this.username} şu an canlı yayında değil ya da kullanıcı adı yanlış. Önce TikTok LIVE Studio'da yayını başlat; köprü kendiliğinden tekrar dener.`;
      else this.status = 'bağlanamadı: ' + (err && err.message ? err.message : String(err));
      this.log('TikTok bağlanamadı:', this.status);
      this.retry();
    });
  }

  retry(fixedSec) {
    if (!this.wanted) return;
    clearTimeout(this.retryTimer);
    const sec = fixedSec || Math.min(60, 10 * Math.pow(2, this.retries++));
    const generation = this.generation;
    this.retryTimer = setTimeout(async () => {
      if (!this.wanted || generation !== this.generation) return;
      const old = this.conn;
      this.conn = null;
      if (old) { try { await old.disconnect(); } catch {} }
      // A newer start/stop can run while disconnect is pending. It owns the connection now.
      if (this.wanted && generation === this.generation && this.conn == null) this.connect();
    }, sec * 1000);
  }

}
