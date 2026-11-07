// Gift pictures straight from TikTok (user request): when the bridge is connected to the LIVE, it reads the room's
// gift list once, picks one gift per tier (coin range) — popular gifts first — downloads the official pictures into
// the game's StreamingAssets/GiftIcons and writes the file names into config.json (all game copies). The running game
// reloads config.json within 2 seconds and shows the pictures. A tier that already has a picture is left alone.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';

// popular gifts first (any gift in the coin range still triggers the same move)
const PREFERRED = ['Rose', 'TikTok', 'Finger Heart', 'Doughnut', 'Hand Hearts', 'Confetti', 'Money Gun', 'Galaxy',
  'Fireworks', 'Whale diving', 'Motorcycle', 'Rosa Nebula', 'Lion', 'Universe'];

export function gameTargets(projectRoot) {
  const list = [
    path.join(projectRoot, 'game', 'Assets', 'StreamingAssets'),
    path.join(projectRoot, 'Builds', 'Windows', 'Arena Dovusu_Data', 'StreamingAssets'),
    path.join(os.homedir(), 'OneDrive', 'Desktop', 'TiktokLive', 'TikTokLiveArenaFight', 'Arena Dovusu_Data', 'StreamingAssets'),
  ];
  return list.filter((d) => fs.existsSync(path.join(d, 'config.json')));
}

function giftList(raw) {
  const arr = Array.isArray(raw) ? raw : raw?.gifts || raw?.data?.gifts || raw?.giftList || [];
  return arr.map((g) => ({
    name: g.name || g.giftName || '',
    coins: Number(g.diamond_count ?? g.diamondCount ?? 0),
    urls: [...(g.image?.url_list || g.image?.urlList || []), ...(g.icon?.url_list || g.icon?.urlList || [])],
  })).filter((g) => g.name && g.coins > 0 && g.urls.length);
}

// Unity can only read PNG/JPG. TikTok often serves .webp; its CDN converts when the name ends in .png instead.
async function downloadPicture(urls) {
  const tries = [];
  for (const u of urls) { if (/\.png/i.test(u)) tries.push(u); }
  for (const u of urls) tries.push(u.replace(/\.webp/i, '.png'));
  for (const u of urls) tries.push(u);
  for (const u of [...new Set(tries)]) {
    try {
      const res = await fetch(u);
      if (!res.ok) continue;
      const b = Buffer.from(await res.arrayBuffer());
      const png = b[0] === 0x89 && b[1] === 0x50 && b[2] === 0x4e && b[3] === 0x47, jpg = b[0] === 0xff && b[1] === 0xd8;
      if (png || jpg) return b;
    } catch {}
  }
  return null;
}

const safe = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'gift';

export async function installGiftImages(rawGifts, projectRoot, log) {
  const targets = gameTargets(projectRoot);
  if (!targets.length) return;
  const cfg = JSON.parse(fs.readFileSync(path.join(targets[0], 'config.json'), 'utf8'));
  const tiers = cfg?.gifts?.tiers || [];
  if (!tiers.length || tiers.every((t) => t.icon)) return;
  const gifts = giftList(rawGifts);
  if (!gifts.length) { log('Hediye listesi boş geldi'); return; }
  const picks = tiers.map((t, i) => {
    if (t.icon) return null;
    const lo = t.minCoins, hi = i + 1 < tiers.length ? tiers[i + 1].minCoins : Infinity;
    const inRange = gifts.filter((g) => g.coins >= lo && g.coins < hi);
    if (!inRange.length) return null;
    inRange.sort((a, b) => {
      const pa = PREFERRED.indexOf(a.name), pb = PREFERRED.indexOf(b.name);
      if ((pa >= 0) !== (pb >= 0)) return pa >= 0 ? -1 : 1;
      if (pa >= 0 && pb >= 0) return pa - pb;
      return a.coins - b.coins; // cheapest = sent most often
    });
    return inRange[0];
  });
  for (let i = 0; i < picks.length; i++) {
    const g = picks[i];
    if (!g) continue;
    const file = `${safe(g.name)}.png`;
    try {
      const bytes = await downloadPicture(g.urls);
      if (!bytes) throw new Error('PNG/JPG bulunamadı');
      for (const dir of targets) {
        fs.mkdirSync(path.join(dir, 'GiftIcons'), { recursive: true });
        fs.writeFileSync(path.join(dir, 'GiftIcons', file), bytes);
      }
      tiers[i].icon = file;
      log(`Hediye resmi eklendi: ${g.name} (${g.coins} coin) -> ${tiers[i].label}`);
    } catch (e) { log('Hediye resmi indirilemedi:', g.name, e.message); }
  }
  // write the icon names into every game copy's config.json (keeps everything else as it is)
  for (const dir of targets) {
    try {
      const p = path.join(dir, 'config.json');
      const c = JSON.parse(fs.readFileSync(p, 'utf8'));
      c.gifts.tiers.forEach((t, i) => { if (!t.icon && tiers[i]?.icon) t.icon = tiers[i].icon; });
      fs.writeFileSync(p, JSON.stringify(c, null, 2));
    } catch (e) { log('config.json güncellenemedi:', dir, e.message); }
  }
}
