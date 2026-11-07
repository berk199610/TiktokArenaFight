// Launcher helper: reuse a healthy bridge, otherwise start it without an extra console window.
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export async function bridgeIsReady(port) {
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/status`, { signal: AbortSignal.timeout(700) });
    if (!response.ok) return false;
    const status = await response.json();
    return typeof status.gameConnected === 'boolean' && typeof status.eventsTotal === 'number';
  } catch { return false; }
}

export async function ensureBridge(port = 8788) {
  if (await bridgeIsReady(port)) return { started: false };
  const child = spawn(process.execPath, [path.join(projectRoot, 'bridge/src/server.js')], {
    cwd: projectRoot,
    detached: true,
    stdio: 'ignore',
    windowsHide: true,
    env: { ...process.env, PORT: String(port) },
  });
  let startError;
  child.on('error', (error) => { startError = error; });
  child.unref();
  for (let attempt = 0; attempt < 40; attempt++) {
    if (startError) throw startError;
    if (await bridgeIsReady(port)) return { started: true, pid: child.pid };
    await pause(150);
  }
  throw new Error('Kopru baslatilamadi. 8788 portunu kullanan baska bir program olup olmadigini kontrol et.');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const result = await ensureBridge();
    console.log(result.started ? 'Kopru arka planda baslatildi.' : 'Calisan kopru kullaniliyor.');
  } catch (error) {
    console.error('[HATA] ' + error.message);
    process.exitCode = 1;
  }
}
