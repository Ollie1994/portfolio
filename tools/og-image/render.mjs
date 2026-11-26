// Renders og-image.html to src/Portfolio.Client/wwwroot/img/og-image.png at 1200x630.
//
//   node tools/og-image/render.mjs
//
// Needs Node 22+ (for the built-in WebSocket) and Chrome. Drives Chrome over its debugging
// protocol rather than using `chrome --screenshot`, which on Windows sometimes exits
// without writing a file. Text is rendered without subpixel anti-aliasing
// (--disable-lcd-text): its colour fringes turn into blur once a platform rescales and
// re-compresses the card.

import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = pathToFileURL(join(here, 'og-image.html')).href;
const target = resolve(here, '..', '..', 'src', 'Portfolio.Client', 'wwwroot', 'img', 'og-image.png');

const chromePaths = [
    process.env.CHROME_PATH,
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/usr/bin/google-chrome'
].filter(Boolean);
const chrome = chromePaths.find(p => existsSync(p));
if (!chrome) {
    throw new Error('Chrome not found; set CHROME_PATH.');
}

const port = 9200 + Math.floor(Math.random() * 500);
const profile = mkdtempSync(join(tmpdir(), 'og-image-'));
const browser = spawn(chrome, [
    '--headless=new', '--disable-gpu', '--disable-lcd-text', '--hide-scrollbars', '--no-first-run',
    `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, 'about:blank'
], { stdio: 'ignore' });

const sleep = ms => new Promise(r => setTimeout(r, ms));

try {
    let page;
    for (let i = 0; i < 40 && !page; i++) {
        try {
            page = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page');
        } catch {
            // Chrome is still starting.
        }
        if (!page) await sleep(250);
    }
    if (!page) throw new Error('Chrome did not start.');

    const ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((ok, fail) => { ws.onopen = ok; ws.onerror = fail; });
    let id = 0;
    const pending = new Map();
    ws.onmessage = m => {
        const d = JSON.parse(m.data);
        if (d.id && pending.has(d.id)) {
            pending.get(d.id)(d);
            pending.delete(d.id);
        }
    };
    const send = (method, params = {}) => new Promise((ok, fail) => {
        const i = ++id;
        pending.set(i, d => (d.error ? fail(new Error(d.error.message)) : ok(d.result)));
        ws.send(JSON.stringify({ id: i, method, params }));
    });

    await send('Page.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1200, height: 630, deviceScaleFactor: 1, mobile: false });
    await send('Page.navigate', { url: source });
    await sleep(1500); // Fonts are local, but give layout a moment.

    const shot = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: 1200, height: 630, scale: 1 } });
    writeFileSync(target, Buffer.from(shot.data, 'base64'));
    console.log(`Wrote ${target}`);
    await send('Browser.close').catch(() => {});
} finally {
    browser.kill();
    await sleep(500);
    rmSync(profile, { recursive: true, force: true, maxRetries: 3 });
}
