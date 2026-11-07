import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import net from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { WebSocket } from 'ws';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

test('local bridge delivers all eight gift tiers, likes, and comments over the real game socket', { timeout: 15000 }, async (t) => {
  const probe = net.createServer();
  probe.listen(0, '127.0.0.1');
  await once(probe, 'listening');
  const port = probe.address().port;
  await new Promise((resolve) => probe.close(resolve));
  const proc = spawn(process.execPath, [path.join(root, 'src/server.js')], {
    cwd: root, env: { ...process.env, PORT: String(port) }, windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const errors = [];
  proc.stderr.on('data', (data) => errors.push(data.toString()));
  t.after(async () => {
    if (proc.exitCode === null) {
      proc.kill();
      await once(proc, 'exit');
    }
  });
  let ready = false;
  for (let i = 0; i < 60; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${port}/api/status`);
      if (res.ok) { ready = true; break; }
    } catch {}
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  assert.ok(ready, errors.join('\n'));
  const ws = new WebSocket(`ws://127.0.0.1:${port}/game`);
  t.after(() => ws.terminate());
  await once(ws, 'open');
  const received = [];
  ws.on('message', (buf) => {
    const msg = JSON.parse(buf.toString());
    if (msg.t === 'evs') received.push(...msg.list);
  });
  const post = (body) => fetch(`http://127.0.0.1:${port}/api/sim`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
  for (let tier = 0; tier < 8; tier++) await post({ action: 'gift', user: 'Testİzleyici', tier });
  await post({ action: 'like', user: 'Testİzleyici', count: 5 });
  await post({ action: 'comment', user: 'Testİzleyici' });
  for (let i = 0; received.length < 10 && i < 30; i++) await new Promise((resolve) => setTimeout(resolve, 50));
  assert.equal(received.length, 10);
  assert.deepEqual(received.filter((e) => e.type === 'gift').map((e) => e.coins), [1, 5, 30, 100, 300, 1000, 2500, 5000]);
  assert.ok(received.every((e) => e.userId === 'manual:Testİzleyici'));
  assert.equal(received.find((e) => e.type === 'like').count, 5);
  const status = await (await fetch(`http://127.0.0.1:${port}/api/status`)).json();
  assert.equal(status.gameConnected, true);
  assert.equal(status.eventsTotal, 10);
  assert.equal(status.tiktok.connected, false);
  const invalid = await fetch(`http://127.0.0.1:${port}/api/sim`, { method: 'POST', body: '{bad' });
  assert.equal(invalid.status, 400);
  assert.equal(errors.length, 0, errors.join('\n'));
});
