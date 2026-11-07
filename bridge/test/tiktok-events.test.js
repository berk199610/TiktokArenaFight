import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { WebcastEvent } from 'tiktok-live-connector';
import { GiftEvents, readUser } from '../src/tiktok-events.js';
import { TikTokProvider } from '../src/tiktok.js';

const user = { id: 7234567890123456789n, displayId: 'viewer_01', nickname: 'İzleyici' };
const rose = (repeatCount, repeatEnd = 0, extra = {}) => ({
  user, giftId: 5655, gift: { type: 1, name: 'Rose', diamondCount: 1 },
  repeatCount, repeatEnd, groupId: 'combo-1', ...extra,
});
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('current protobuf v3 user identity stays exact and identical across event types', () => {
  const u = readUser({ user });
  assert.equal(u.userId, 'tt:7234567890123456789');
  assert.equal(u.nickname, 'İzleyici');
  assert.equal(readUser({ user: { id: 0n, idStr: '7234567890123456789' } }).userId, u.userId);
  assert.equal(readUser({ user: { id: 0n, displayId: 'handle' } }).userId, 'tt:handle');
  assert.equal(readUser({ user: {} }), null);
});

test('documented legacy flattened gifts retain value, name, and viewer', () => {
  const events = new GiftEvents();
  const e = events.normalize({ userId: '42', nickname: 'Eski', giftId: 9, gift: { gift_type: 0 },
    giftName: 'Galaxy', diamondCount: 1000, repeatCount: 2, repeatEnd: true });
  assert.deepEqual(e, { type: 'gift', userId: 'tt:42', nickname: 'Eski', avatarUrl: '', coins: 1000, giftName: 'Galaxy', count: 2 });
});

test('incomplete gift data falls back field by field to catalog metadata', () => {
  const events = new GiftEvents();
  const e = events.normalize({ user, giftId: 99, gift: { type: 0, diamondCount: 0 },
    extendedGiftInfo: { name: 'Lion', diamond_count: 29999, type: 0 }, repeatCount: 1 });
  assert.equal(e.coins, 29999);
  assert.equal(e.giftName, 'Lion');
});

test('growing streak gives immediate deltas and counts completed/replayed frames once', () => {
  const events = new GiftEvents();
  assert.equal(events.normalize(rose(1)).count, 1);
  assert.equal(events.normalize(rose(2)).count, 1);
  assert.equal(events.normalize(rose(5)).count, 3);
  assert.equal(events.normalize(rose(5, 1)), null);
  assert.equal(events.normalize(rose(5, 1)), null);
  assert.equal(events.normalize(rose(3, 0)), null);
  assert.equal(events.normalize(rose(1, 1, { groupId: 'combo-2' })).count, 1);
});

test('final-only streak and numeric/string completion flags do not double count', () => {
  const events = new GiftEvents();
  assert.equal(events.normalize(rose(7, '1')).count, 7);
  assert.equal(events.normalize(rose(7, true)), null);
});

test('concurrent senders and rooms have separate streaks', () => {
  const events = new GiftEvents();
  assert.equal(events.normalize(rose(2), 'room-a').count, 2);
  assert.equal(events.normalize(rose(2), 'room-b').count, 2);
  assert.equal(events.normalize(rose(2, 0, { user: { id: 23n } }), 'room-a').count, 2);
});

test('single-shot replay is ignored while separate copies of the same gift all count', () => {
  const events = new GiftEvents();
  const gift = { user, giftId: 5, gift: { name: 'Galaxy', type: 0, diamondCount: 1000 }, repeatCount: 1, common: { msgId: 1n } };
  assert.equal(events.normalize(gift).count, 1);
  assert.equal(events.normalize(gift), null);
  assert.equal(events.normalize({ ...gift, common: { msgId: 2n } }).count, 1);
});

test('legacy streak without groupId starts afresh on the next growing sequence', () => {
  const events = new GiftEvents();
  assert.equal(events.normalize(rose(1, 0, { groupId: '' })).count, 1);
  assert.equal(events.normalize(rose(2, 1, { groupId: '' })).count, 1);
  assert.equal(events.normalize(rose(1, 0, { groupId: '' })).count, 1);
  assert.equal(events.normalize(rose(1, 1, { groupId: '' })), null);
});

class FakeConnection extends EventEmitter {
  constructor() { super(); this.roomId = 'room-test'; }
  connect() { return Promise.resolve({ roomId: this.roomId }); }
  disconnect() { this.disconnected = true; return Promise.resolve(); }
  fetchAvailableGifts() { return Promise.resolve([{ id: 1, name: 'Gift', diamond_count: 100 }]); }
}

function fixture(t, createConnection = () => new FakeConnection()) {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'arena-tiktok-test-'));
  const sent = [], connections = [];
  const provider = new TikTokProvider((e) => sent.push(e), () => {}, temp, (username, options) => {
    const conn = createConnection();
    conn.options = options;
    connections.push(conn);
    return conn;
  });
  t.after(async () => {
    await provider.stop();
    clearInterval(provider.cleanupTimer);
    fs.rmSync(temp, { recursive: true, force: true });
  });
  return { provider, connections, sent };
}

test('provider converts comments/likes/gifts to the Unity envelope with a shared userId', async (t) => {
  const { provider, connections, sent } = fixture(t);
  await provider.start('@host');
  await settle();
  const conn = connections[0];
  conn.emit(WebcastEvent.CHAT, { user, content: 'hadi' });
  conn.emit(WebcastEvent.LIKE, { user, count: 5 });
  conn.emit(WebcastEvent.GIFT, rose(1));
  conn.emit(WebcastEvent.GIFT, rose(1, 1));
  assert.deepEqual(sent.map((e) => e.type), ['comment', 'like', 'gift']);
  assert.equal(new Set(sent.map((e) => e.userId)).size, 1);
  assert.equal(sent[1].count, 5);
  assert.equal(sent[2].coins, 1);
  assert.ok(sent.every((e) => Number.isFinite(e.ts)));
  assert.equal(provider.info().counts.gift, 1);
  assert.equal(conn.options.enableExtendedGiftInfo, false);
});

test('optional gift catalog failure does not disconnect an otherwise connected LIVE', async (t) => {
  const { provider, connections, sent } = fixture(t, () => {
    const c = new FakeConnection();
    c.fetchAvailableGifts = () => Promise.reject(new Error('catalog unavailable'));
    return c;
  });
  await provider.start('host');
  await settle();
  assert.equal(provider.connected, true);
  connections[0].emit(WebcastEvent.GIFT, rose(1));
  assert.equal(sent[0].coins, 1);
});

test('retired connection cannot send events or revive status after stop', async (t) => {
  let resolve;
  const { provider, connections, sent } = fixture(t, () => {
    const c = new FakeConnection();
    c.connect = () => new Promise((r) => { resolve = r; });
    return c;
  });
  await provider.start('host');
  await provider.stop();
  connections[0].emit(WebcastEvent.GIFT, rose(1));
  resolve({ roomId: 'retired' });
  await settle();
  assert.equal(provider.connected, false);
  assert.equal(provider.status, 'kapalı');
  assert.equal(sent.length, 0);
});

test('start during an in-flight retry disconnect does not create an orphan connection', async (t) => {
  const { provider, connections } = fixture(t);
  await provider.start('first');
  await settle();
  let finishDisconnect, noteDisconnect;
  const disconnecting = new Promise((resolve) => { noteDisconnect = resolve; });
  connections[0].disconnect = () => new Promise((resolve) => {
    finishDisconnect = resolve;
    noteDisconnect();
  });
  provider.retry(0.001);
  await disconnecting;
  await provider.start('latest');
  await settle();
  const newest = provider.conn;
  finishDisconnect();
  await settle();
  assert.equal(connections.length, 2);
  assert.equal(provider.conn, newest);
  assert.equal(provider.username, 'latest');
  assert.equal(provider.connected, true);
});

test('a newer start wins when the previous start is awaiting disconnect', async (t) => {
  const { provider, connections } = fixture(t);
  await provider.start('first');
  await settle();
  let finishDisconnect;
  connections[0].disconnect = () => new Promise((resolve) => { finishDisconnect = resolve; });
  const olderStart = provider.start('superseded');
  await provider.start('latest');
  await settle();
  const newest = provider.conn;
  finishDisconnect();
  await olderStart;
  await settle();
  assert.equal(connections.length, 2);
  assert.equal(provider.conn, newest);
  assert.equal(provider.username, 'latest');
});

test('stop during an in-flight retry disconnect stays stopped', async (t) => {
  const { provider, connections } = fixture(t);
  await provider.start('first');
  await settle();
  let finishDisconnect, noteDisconnect;
  const disconnecting = new Promise((resolve) => { noteDisconnect = resolve; });
  connections[0].disconnect = () => new Promise((resolve) => { finishDisconnect = resolve; noteDisconnect(); });
  provider.retry(0.001);
  await disconnecting;
  await provider.stop();
  finishDisconnect();
  await settle();
  assert.equal(connections.length, 1);
  assert.equal(provider.conn, null);
  assert.equal(provider.wanted, false);
  assert.equal(provider.connected, false);
  assert.equal(provider.status, 'kapalı');
});
