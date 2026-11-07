// Arena Dövüşü — köprü sunucusu (DESIGN §10.2).
//   ws://127.0.0.1:8788/game   -> Unity oyunu bağlanır (olaylar buradan gider)
//   ws://127.0.0.1:8788/panel  -> yayıncı / simülatör paneli
//   http://127.0.0.1:8788/     -> paneli tarayıcıda aç
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import { Simulator } from './simulator.js';
import { TikTokProvider } from './tiktok.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const cfg = JSON.parse(fs.readFileSync(path.join(root, 'config.json'), 'utf8'));
const port = Number(process.env.PORT || cfg.port || 8788);

const games = new Set();
const panels = new Set();
let eventsThisSecond = 0, eventsPerSecond = 0, eventsTotal = 0;
const log = (...a) => console.log(new Date().toLocaleTimeString('tr-TR'), ...a);

function send(set, obj) {
  const s = JSON.stringify(obj);
  for (const ws of set) if (ws.readyState === 1) ws.send(s);
}

// Every viewer event goes through here, whatever the source (simulator or TikTok).
// Schema: { type: gift|like|comment|follow|share|join, userId, nickname, avatarUrl, coins, giftName, count, text, ts }
const queue = [];
function emitEvent(e) {
  queue.push(e);
  eventsThisSecond++;
  eventsTotal++;
  if (cfg.logEvents) log('olay', e.type, e.nickname, e.coins || e.count || e.text || '');
}
// batch every 50 ms so a big crowd does not flood the socket
setInterval(() => {
  if (queue.length === 0) return;
  const list = queue.splice(0, queue.length);
  send(games, { t: 'evs', list });
}, 50);
setInterval(() => { eventsPerSecond = eventsThisSecond; eventsThisSecond = 0; broadcastStatus(); }, 1000);

const sim = new Simulator(emitEvent);
const tiktok = new TikTokProvider(emitEvent, log);
if (cfg.provider === 'tiktok') tiktok.start(cfg.tiktokUsername);

function broadcastStatus() {
  send(panels, {
    t: 'status',
    gameConnected: games.size > 0,
    provider: cfg.provider,
    tiktokConnected: tiktok.connected,
    eventsPerSecond,
    eventsTotal,
    bot: sim.info(),
  });
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname === '/' || url.pathname === '/panel') {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
    fs.createReadStream(path.join(root, 'public', 'panel.html')).pipe(res);
    return;
  }
  if (url.pathname === '/api/status') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ gameConnected: games.size > 0, eventsTotal }));
    return;
  }
  // tiny local API so scripts can drive the simulator without a browser
  if (url.pathname === '/api/sim' && req.method === 'POST') return readJson(req, (body) => { sim.handle(body.action, body); res.end('ok'); });
  if (url.pathname === '/api/cmd' && req.method === 'POST') return readJson(req, (body) => { send(games, { t: 'cmd', cmd: body.cmd, arg: body.arg || '' }); res.end('ok'); });
  res.writeHead(404); res.end('not found');
});

function readJson(req, cb) {
  let data = '';
  req.on('data', (c) => (data += c));
  req.on('end', () => { try { cb(JSON.parse(data || '{}')); } catch (e) { log('API hatası', e.message); } });
}

const wssGame = new WebSocketServer({ noServer: true });
const wssPanel = new WebSocketServer({ noServer: true });

server.on('upgrade', (req, socket, head) => {
  const p = new URL(req.url, 'http://localhost').pathname;
  if (p === '/game') wssGame.handleUpgrade(req, socket, head, (ws) => wssGame.emit('connection', ws, req));
  else if (p === '/panel') wssPanel.handleUpgrade(req, socket, head, (ws) => wssPanel.emit('connection', ws, req));
  else socket.destroy();
});

wssGame.on('connection', (ws) => {
  games.add(ws);
  log('Oyun bağlandı');
  ws.on('close', () => { games.delete(ws); log('Oyun bağlantısı kapandı'); });
});

wssPanel.on('connection', (ws) => {
  panels.add(ws);
  ws.on('message', (buf) => {
    let m; try { m = JSON.parse(buf.toString()); } catch { return; }
    if (m.t === 'sim') sim.handle(m.action, m);
    else if (m.t === 'cmd') send(games, { t: 'cmd', cmd: m.cmd, arg: m.arg || '' });
  });
  ws.on('close', () => panels.delete(ws));
  broadcastStatus();
});

const host = cfg.lan ? '0.0.0.0' : '127.0.0.1';
server.listen(port, host, () => {
  const ips = Object.values(os.networkInterfaces()).flat().filter((i) => i && i.family === 'IPv4' && !i.internal).map((i) => i.address);
  log(`Köprü hazır. Panel: http://localhost:${port}` + (cfg.lan && ips.length ? `  (telefondan: http://${ips[0]}:${port})` : ''));
  log(`Kaynak: ${cfg.provider === 'tiktok' ? 'TikTok LIVE (@' + cfg.tiktokUsername + ')' : 'Simülatör'}`);
});
