// Simülatör (DESIGN §10.2): düğmelerle sahte yorum / beğeni / hediye ve "kalabalık botu"
// (seçilen sayıda sahte izleyiciyle gerçekçi trafik). Oyun TikTok olmadan buradan test edilir.

const FIRST = ['ahmet', 'mehmet', 'ayse', 'fatma', 'zeynep', 'emre', 'burak', 'elif', 'deniz', 'cem', 'selin', 'berk', 'ece', 'mert', 'irem', 'kaan', 'naz', 'efe', 'derin', 'arda'];
const TAIL = ['34', '06', '35', '_tr', '.x', '_01', '07', '_kral', '.official', '61', '42', '_gs', '_fb', '16'];
const TIERS = [1, 5, 30, 100, 300, 1000, 5000];

export class Simulator {
  constructor(emit) {
    this.emit = emit;
    this.viewers = 0;
    this.rate = 6;           // events per second at 60 viewers, scales with viewer count
    this.giftShare = 0.08;
    this.timer = setInterval(() => this.tick(), 100);
    this.acc = 0;
  }

  info() { return { viewers: this.viewers, rate: this.rate, giftShare: this.giftShare }; }

  name(i) { return FIRST[(i * 7) % FIRST.length] + TAIL[(i * 13) % TAIL.length] + (i >= 280 ? i : ''); }

  ev(type, i, extra = {}) {
    this.emit({ type, userId: 'sim:' + i, nickname: this.name(i), avatarUrl: '', coins: 0, giftName: '', count: 1, text: '', ts: Date.now(), ...extra });
  }

  tick() {
    if (this.viewers <= 0) return;
    this.acc += 0.1 * this.rate * Math.max(0.2, this.viewers / 60);
    while (this.acc >= 1) {
      this.acc -= 1;
      const i = Math.floor(Math.random() * this.viewers);
      const r = Math.random();
      if (r < this.giftShare) {
        const g = Math.random();
        const coins = g < 0.55 ? 1 : g < 0.75 ? 5 : g < 0.87 ? 30 : g < 0.94 ? 100 : g < 0.98 ? 300 : g < 0.995 ? 1000 : 5000;
        this.ev('gift', i, { coins, giftName: 'Sim', count: 1 });
      } else if (r < 0.5) this.ev('comment', i, { text: 'hadi!' });
      else this.ev('like', i, { count: 1 + Math.floor(Math.random() * 5) });
    }
  }

  handle(action, m = {}) {
    const who = (m.user || '').trim();
    const id = who ? 'manual:' + who : 'manual:Berk';
    const nick = who || 'Berk';
    switch (action) {
      case 'viewers': this.viewers = Math.max(0, Math.min(2000, Number(m.value) || 0)); break;
      case 'rate': this.rate = Math.max(0, Number(m.value) || 0); break;
      case 'giftShare': this.giftShare = Math.max(0, Math.min(1, Number(m.value) || 0)); break;
      case 'bot': this.viewers = Number(m.viewers) || 0; break;
      case 'comment': this.emit({ type: 'comment', userId: id, nickname: nick, text: 'test', count: 1, coins: 0, ts: Date.now() }); break;
      case 'like': this.emit({ type: 'like', userId: id, nickname: nick, count: Number(m.count) || 1, coins: 0, ts: Date.now() }); break;
      case 'gift': {
        const tier = Math.max(0, Math.min(TIERS.length - 1, Number(m.tier) || 0));
        this.emit({ type: 'gift', userId: id, nickname: nick, coins: TIERS[tier], giftName: 'Test', count: Number(m.count) || 1, ts: Date.now() });
        break;
      }
      case 'reset': break;
    }
  }
}
