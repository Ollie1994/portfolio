// Renders og-image.html to src/Portfolio.Client/wwwroot/img/og-image.png at 1200x630.
//
//   node tools/og-image/render.mjs
//
// Needs Node 22+ (for the built-in WebSocket), Chrome, and the Segoe UI font: the layout is
// measured in it, so the script refuses to write the card if the font is missing rather than
// overwrite it with a shifted one.
//
// Drives its own headless Chrome over the debugging protocol rather than using
// `chrome --screenshot`, which on Windows sometimes exits without writing a file. Chrome
// picks a free debugging port itself, so this never talks to a browser that is already
// running. Text is rendered without subpixel anti-aliasing (--disable-lcd-text): its colour
// fringes turn into blur once a platform rescales and re-compresses the card.

import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = pathToFileURL(join(here, 'og-image.html')).href;
const target = resolve(here, '..', '..', 'src', 'Portfolio.Client', 'wwwroot', 'img', 'og-image.png');
const StepTimeoutMs = 15000;

const chrome = [
    process.env.CHROME_PATH,
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/usr/bin/google-chrome'
].filter(Boolean).find(p => existsSync(p));
if (!chrome) {
    throw new Error('Chrome not found; set CHROME_PATH.');
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

// The timer is cleared once the step settles, or it would keep Node running after the
// render has finished.
const withTimeout = (promise, what) => {
    let timer;
    const timeout = new Promise((_, fail) => {
        timer = setTimeout(() => fail(new Error(`Timed out: ${what}.`)), StepTimeoutMs);
    });
    return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
};

const profile = mkdtempSync(join(tmpdir(), 'og-image-'));
const browser = spawn(chrome, [
    '--headless=new', '--disable-gpu', '--disable-lcd-text', '--hide-scrollbars', '--no-first-run',
    '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'
], { stdio: 'ignore' });
const exited = new Promise(r => browser.once('exit', r));

try {
    // Port 0 lets Chrome choose a free port, which it writes to DevToolsActivePort in its
    // own profile. Reading it from there means the port is certainly this Chrome's.
    const portFile = join(profile, 'DevToolsActivePort');
    let port;
    for (let i = 0; i < 80 && !port; i++) {
        if (existsSync(portFile)) {
            port = Number(readFileSync(portFile, 'utf8').split('\n')[0]) || undefined;
        }
        if (!port) await sleep(250);
    }
    if (!port) throw new Error('Chrome did not start.');

    const page = (await withTimeout(fetch(`http://127.0.0.1:${port}/json`).then(r => r.json()), 'listing pages'))
        .find(t => t.type === 'page');
    if (!page) throw new Error('Chrome has no page to render into.');

    const ws = new WebSocket(page.webSocketDebuggerUrl);
    await withTimeout(new Promise((ok, fail) => { ws.onopen = ok; ws.onerror = fail; }), 'connecting to Chrome');

    let id = 0;
    const pending = new Map();
    const listeners = new Map();
    ws.onmessage = m => {
        const d = JSON.parse(m.data);
        if (d.id && pending.has(d.id)) {
            pending.get(d.id)(d);
            pending.delete(d.id);
        } else if (d.method && listeners.has(d.method)) {
            listeners.get(d.method)(d.params);
        }
    };
    // If Chrome goes away mid-render, fail every call still waiting instead of hanging.
    ws.onclose = () => {
        for (const settle of pending.values()) settle({ error: { message: 'Chrome closed the connection.' } });
        pending.clear();
    };
    const send = (method, params = {}) => withTimeout(new Promise((ok, fail) => {
        const i = ++id;
        pending.set(i, d => (d.error ? fail(new Error(`${method}: ${d.error.message}`)) : ok(d.result)));
        ws.send(JSON.stringify({ id: i, method, params }));
    }), method);
    const evaluate = async expression => {
        const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
        if (r.exceptionDetails) throw new Error(`In the page: ${r.exceptionDetails.text}`);
        return r.result.value;
    };

    await send('Page.enable');
    await send('Runtime.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1200, height: 630, deviceScaleFactor: 1, mobile: false });

    const loaded = new Promise(r => listeners.set('Page.loadEventFired', r));
    const navigation = await send('Page.navigate', { url: source });
    if (navigation.errorText) throw new Error(`Could not open ${source}: ${navigation.errorText}`);
    await withTimeout(loaded, 'loading og-image.html');
    await evaluate('document.fonts.ready.then(() => true)');

    // document.fonts.check() says yes for any font that is not a web font, installed or
    // not, so compare widths instead: if Segoe UI were missing, text set in it would
    // measure exactly like the fallback.
    const hasSegoe = await evaluate(`(() => {
        const ctx = document.createElement('canvas').getContext('2d');
        const width = font => { ctx.font = font; return ctx.measureText('Oliwer Karlsson · Väm').width; };
        return ['monospace', 'serif'].every(f => width('700 94px "Segoe UI", ' + f) !== width('700 94px ' + f));
    })()`);
    if (!hasSegoe) {
        throw new Error('Segoe UI is not installed, so the card would not match its measured layout. Render it on Windows.');
    }

    // Two animation frames, so the loaded page has been painted before the capture.
    await evaluate('new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => r(true))))');

    const shot = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: 1200, height: 630, scale: 1 } });
    writeFileSync(target, Buffer.from(shot.data, 'base64'));
    console.log(`Wrote ${target}`);
    await send('Browser.close').catch(() => {});
} finally {
    if (browser.exitCode === null) browser.kill();
    let wait;
    await Promise.race([exited, new Promise(r => { wait = setTimeout(r, 5000); })]);
    clearTimeout(wait);
    try {
        rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
    } catch (e) {
        // The card is already written (or the real error is already on its way out), so a
        // locked temp folder is only worth a warning.
        console.warn(`Could not remove the temporary Chrome profile ${profile}: ${e.message}`);
    }
}
