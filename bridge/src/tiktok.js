// TikTok LIVE sağlayıcısı — Aşama 5'te bağlanacak (DESIGN §10.2, §11).
//
//  * Bağlantı tek dosyada durur; kütüphane bozulursa sadece burası değişir, oyun etkilenmez.
//  * Her TikTok olayı simülatörle AYNI şemaya çevrilir:
//      { type: 'gift'|'like'|'comment'|'follow'|'share'|'join', userId, nickname, avatarUrl,
//        coins, giftName, count, text, ts }
//    coins = TEK hediyenin coin değeri, count = seri bittiğinde toplam adet.
//  * Hediye serileri (combo): seri bitince BİR kez gönderilir — her hediye bir kez sayılır, iki kez değil.
//  * Bağlantı koparsa yeniden bağlanır.
//
// Aşama 5'te: `tiktok-live-connector` paketinin GÜNCEL belgesini okuyup olay ve alan adlarını oradan almak
// (tahmin etmek yok).

export class TikTokProvider {
  constructor(emit, log) {
    this.emit = emit;
    this.log = log;
    this.connected = false;
  }

  async start(username) {
    this.log(`TikTok sağlayıcısı henüz bağlı değil (Aşama 5). Kullanıcı: ${username || '(boş)'}`);
    this.connected = false;
    return false;
  }

  stop() { this.connected = false; }
}
