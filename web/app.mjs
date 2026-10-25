import {MAX_BYTES, MAX_FILES, extension, stem, safeStem, displaySize, uniqueName, pairLyrics, collectDirectory, writeToDirectory} from './files.mjs';
const $ = id => document.getElementById(id);
const ENGINE_CACHE = 'qz-engine-0.12.10';
const ENGINE_FILES = ['vendor/ffmpeg-core.js', 'vendor/ffmpeg-core.wasm', 'vendor/aes.js'];
const tracks = [];
let preferences = {theme: 'acid', bitrate: '320'}, inputDirectory = null, outputDirectory = null;
let running = false, runId = 0, activeTrack = null, lyricTarget = null, installPrompt = null, toastTimer;
try { Object.assign(preferences, JSON.parse(localStorage.getItem('qz-preferences') || '{}')); } catch {}
if (!['acid', 'neon', 'candy', 'studio'].includes(preferences.theme)) preferences.theme = 'acid';
if (!['128', '192', '256', '320'].includes(preferences.bitrate)) preferences.bitrate = '320';
function savePreferences() { try { localStorage.setItem('qz-preferences', JSON.stringify(preferences)); } catch { toast('浏览器未允许保存偏好，下次启动可能需要重新设置'); } }
function toast(message, duration = 6000) { $('toast').textContent = message; $('toast').hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').hidden = true, duration); }
function applyTheme() { document.documentElement.dataset.theme = preferences.theme; $('themeSelect').value = preferences.theme; document.querySelector('meta[name="theme-color"]').content = {acid: '#D5FF3F', neon: '#0C0A18', candy: '#FFF1F6', studio: '#F5F6F8'}[preferences.theme]; }
applyTheme(); $('bitrate').value = preferences.bitrate;
$('themeSelect').addEventListener('change', e => { preferences.theme = e.target.value; applyTheme(); savePreferences(); });
$('bitrate').addEventListener('change', e => { preferences.bitrate = e.target.value; savePreferences(); });
const database = new Promise((resolve, reject) => {
 const request = indexedDB.open('qingzhuan-directories', 1);
 request.onupgradeneeded = () => request.result.createObjectStore('handles');
 request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error);
});
async function dbRead(key) { const db = await database; return new Promise((resolve, reject) => { const req = db.transaction('handles').objectStore('handles').get(key); req.onsuccess = () => resolve(req.result); req.onerror = () => reject(req.error); }); }
async function dbWrite(key, value) { const db = await database; return new Promise((resolve, reject) => { const tx = db.transaction('handles', 'readwrite'); const store = tx.objectStore('handles'); if (value) store.put(value, key); else store.delete(key); tx.oncomplete = resolve; tx.onerror = () => reject(tx.error); tx.onabort = () => reject(tx.error); }); }
function directoryLabels() {
 $('inputDirName').textContent = inputDirectory?.name || '系统文件选择器';
 $('outputDirName').textContent = outputDirectory?.name || '浏览器下载目录';
 $('outputLocation').textContent = outputDirectory?.name || '浏览器下载目录';
 $('saveHint').textContent = outputDirectory ? '完成后直接保存到所选目录，重名自动编号。' : '完成后点击单曲下载，或打包为 ZIP。';
}
const handlesReady = Promise.all([dbRead('input'), dbRead('output')]).then(([a, b]) => { inputDirectory = a || null; outputDirectory = b || null; directoryLabels(); }).catch(() => {});
const directorySupported = 'showDirectoryPicker' in window;
$('directorySupport').textContent = directorySupported ? '目录授权仅用于本地读写。点击“恢复默认”即可忘记目录。' : '当前浏览器不支持记住本地目录；仍可选择文件，并通过浏览器下载或 ZIP 保存。桌面 Chrome / Edge 可使用授权目录。';
$('chooseInputDir').disabled = $('chooseOutputDir').disabled = !directorySupported;
$('directorySettings').addEventListener('click', () => $('directoryDialog').showModal());
async function chooseDirectory(kind) {
 try {
  const old = kind === 'input' ? inputDirectory : outputDirectory;
  const handle = await window.showDirectoryPicker({id: 'qingzhuan-' + kind, mode: kind === 'output' ? 'readwrite' : 'read', ...(old ? {startIn: old} : {})});
  if (kind === 'input') inputDirectory = handle; else outputDirectory = handle;
  directoryLabels();
  try { await dbWrite(kind, handle); toast('默认目录已记住'); } catch { toast('目录可在本次使用，但浏览器未能保存授权'); }
 } catch (e) { if (e.name !== 'AbortError') toast('无法选择目录：' + e.message); }
}
$('chooseInputDir').addEventListener('click', () => chooseDirectory('input'));
$('chooseOutputDir').addEventListener('click', () => chooseDirectory('output'));
for (const kind of ['input', 'output']) $('reset' + (kind === 'input' ? 'Input' : 'Output') + 'Dir').addEventListener('click', async () => { if (kind === 'input') inputDirectory = null; else outputDirectory = null; directoryLabels(); try { await dbWrite(kind, null); } catch {} toast('已恢复默认'); });
function icon(name) { const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); svg.setAttribute('aria-hidden', 'true'); const use = document.createElementNS(svg.namespaceURI, 'use'); use.setAttribute('href', '#i-' + name); svg.append(use); return svg; }
function button(label, action, glyph) { const b = document.createElement('button'); b.type = 'button'; b.className = glyph ? 'icon-button' : 'text-button'; b.title = b.ariaLabel = label; if (glyph) b.append(icon(glyph)); else b.textContent = label; b.addEventListener('click', action); return b; }
function dispose(track) { if (track.url) URL.revokeObjectURL(track.url); if (track.lyricUrl) URL.revokeObjectURL(track.lyricUrl); }
function download(url, name) { const a = document.createElement('a'); a.href = url; a.download = name; document.body.append(a); a.click(); a.remove(); }
function render() {
 pairLyrics(tracks);
 const audio = tracks.filter(t => t.ext !== 'lrc');
 const looseLyrics = tracks.filter(t => t.ext === 'lrc' && !audio.some(a => a.lyric === t.file));
 $('queueCount').textContent = audio.length;
 $('emptyState').hidden = tracks.length > 0;
 $('trackList').replaceChildren();
 for (const track of [...audio, ...looseLyrics]) {
  const row = document.createElement('li'); row.className = 'track-row'; row.dataset.trackId = track.id;
  const tag = document.createElement('span'); tag.className = 'track-type'; tag.textContent = track.ext.toUpperCase();
  const info = document.createElement('div'); info.className = 'track-info';
  const title = document.createElement('span'); title.className = 'track-name'; title.textContent = track.file.name; title.title = track.path;
  const detail = document.createElement('span'); detail.className = 'track-detail'; detail.textContent = displaySize(track.file.size) + (track.ext === 'lrc' ? ' · 等待同名音频' : track.lyric ? ' · 已匹配歌词' : track.lyricAmbiguous ? ' · 多个同名歌词，请手动关联' : ' · 无歌词');
  const state = document.createElement('span'); state.className = 'track-state ' + track.state; state.dataset.status = track.id;
  state.textContent = track.ext === 'lrc' ? '歌词文件，不单独转换' : track.message || '等待转换'; state.title = track.warning || track.message || '';
  info.append(title, detail, state);
  const actions = document.createElement('div'); actions.className = 'row-actions';
  if (track.url) { actions.append(button('下载 MP3', () => download(track.url, track.outputName), 'download')); if (track.lyricUrl) actions.append(button('歌词', () => download(track.lyricUrl, stem(track.outputName) + '.lrc'))); }
  else if (track.ext !== 'lrc') { const b = button('关联歌词', () => { lyricTarget = track; $('lyricInput').click(); }); b.disabled = running; actions.append(b); }
  const remove = button('移除 ' + track.file.name, () => { dispose(track); tracks.splice(tracks.indexOf(track), 1); render(); }, 'close'); remove.classList.add('remove-track'); remove.disabled = running; actions.append(remove);
  row.append(tag, info, actions); $('trackList').append(row);
 }
 $('clearButton').disabled = running || tracks.length === 0;
 $('startButton').disabled = running || !audio.some(t => t.state !== 'done');
 $('startButton').firstChild.textContent = audio.some(t => t.state === 'error' || t.state === 'cancelled') ? '重试未完成' : '开始转换';
 $('downloadAll').disabled = running || !tracks.some(t => t.blob);
 for (const name of ['addFiles', 'addFolder', 'bitrate', 'directorySettings']) $(name).disabled = running;
 $('cancelButton').hidden = !running; document.body.classList.toggle('busy', running);
 $('queueSummary').textContent = tracks.length ? audio.length + ' 首音频 · ' + tracks.filter(t => t.ext === 'lrc').length + ' 份歌词' : '单个文件最大 128 MB · 每批最多 200 个文件';
}
function progress(track, value, label, index, total) {
 track.message = label + ' ' + Math.round(value * 100) + '%';
 const status = document.querySelector('[data-status="' + track.id + '"]'); if (status) status.textContent = track.message;
 const percent = Math.round((index + value) / total * 100);
 $('overallPercent').textContent = percent + '%'; $('overallProgress').value = percent;
}
function addEntries(entries) {
 if (running) { toast('请先完成或取消当前转换'); return; }
 let skipped = 0, tooLarge = 0;
 const seen = new Set(tracks.map(t => t.path + '|' + t.file.size + '|' + t.file.lastModified));
 for (const entry of entries) {
  const file = entry.file || entry, path = entry.path || file.webkitRelativePath || file.name, ext = extension(file.name);
  if (!['ncm', 'flac', 'mp3', 'lrc'].includes(ext)) { skipped++; continue; }
  if (file.size > MAX_BYTES) { tooLarge++; continue; }
  const key = path + '|' + file.size + '|' + file.lastModified;
  if (seen.has(key)) continue;
  if (tracks.length >= MAX_FILES) { skipped++; continue; }
  seen.add(key); tracks.push({id: crypto.randomUUID(), file, path, ext, state: 'waiting', message: ''});
 }
 render();
 if (tooLarge) toast(tooLarge + ' 个文件超过 128 MB，请用桌面版处理');
 else if (skipped) toast('已跳过不支持、超出队列限制的文件');
}
$('fileInput').addEventListener('change', e => { addEntries([...e.target.files]); e.target.value = ''; });
$('folderInput').addEventListener('change', e => { addEntries([...e.target.files]); e.target.value = ''; });
$('lyricInput').addEventListener('change', e => { if (lyricTarget && e.target.files[0]) { lyricTarget.manualLyric = e.target.files[0]; render(); } e.target.value = ''; lyricTarget = null; });
$('addFiles').addEventListener('click', async () => {
 if (!('showOpenFilePicker' in window) || !inputDirectory) { $('fileInput').click(); return; }
 try {
  const handles = await window.showOpenFilePicker({id: 'qingzhuan-files', multiple: true, types: [{description: '音乐与歌词', accept: {'application/octet-stream': ['.ncm', '.flac', '.mp3', '.lrc']}}], ...(inputDirectory ? {startIn: inputDirectory} : {})});
  addEntries(await Promise.all(handles.map(async h => ({file: await h.getFile(), path: h.name}))));
 } catch (e) { if (e.name !== 'AbortError') { toast('系统选择器不可用，请使用文件选择'); $('fileInput').click(); } }
});
$('addFolder').addEventListener('click', async () => {
 if (!directorySupported) { $('folderInput').click(); return; }
 try { const dir = await window.showDirectoryPicker({id: 'qingzhuan-import', mode: 'read', ...(inputDirectory ? {startIn: inputDirectory} : {})}); $('runStatus').textContent = '读取文件夹…'; addEntries(await collectDirectory(dir)); $('runStatus').textContent = '文件已添加'; }
 catch (e) { if (e.name !== 'AbortError') toast('无法读取文件夹：' + e.message); }
});
const dropzone = $('dropzone');
for (const event of ['dragover', 'dragenter']) dropzone.addEventListener(event, e => { e.preventDefault(); if (!running) dropzone.classList.add('dragover'); });
for (const event of ['dragleave', 'drop']) dropzone.addEventListener(event, e => { e.preventDefault(); dropzone.classList.remove('dragover'); });
dropzone.addEventListener('drop', async e => {
 if (running) return;
 const files = [...e.dataTransfer.files];
 const promises = [...e.dataTransfer.items].filter(i => i.kind === 'file').map(i => i.getAsFileSystemHandle?.());
 try { const handles = await Promise.all(promises); if (handles.some(Boolean)) { const entries = []; for (const h of handles) if (h) { if (h.kind === 'directory') entries.push(...await collectDirectory(h, h.name + '/')); else entries.push({file: await h.getFile(), path: h.name}); } addEntries(entries); } else addEntries(files); }
 catch { addEntries(files); }
});
window.addEventListener('dragover', e => e.preventDefault()); window.addEventListener('drop', e => e.preventDefault());
$('clearButton').addEventListener('click', () => { tracks.forEach(dispose); tracks.length = 0; $('overallProgress').value = 0; $('overallPercent').textContent = '0%'; $('runStatus').textContent = '准备好，就出发。'; render(); });
class Bridge {
 worker = null; pending = new Map(); counter = 0;
 request(type, data = {}, onProgress = () => {}) {
  if (!this.worker) {
   this.worker = new Worker(new URL('./converter.js', import.meta.url));
   this.worker.onmessage = ({data}) => { const p = this.pending.get(data.id); if (!p) return; if (data.type === 'progress') { p.onProgress(data.value, data.label); return; } clearTimeout(p.timer); this.pending.delete(data.id); if (data.type === 'error') p.reject(new Error(data.message)); else p.resolve(data); };
   this.worker.onerror = () => { this.stop(new Error('转码组件加载失败，请联网后点击“准备离线转码”再重试')); };
  }
  const id = ++this.counter;
  return new Promise((resolve, reject) => { const timer = setTimeout(() => this.stop(new Error('转换超时，请尝试更小的文件或桌面版')), type === 'load' ? 120000 : 600000); this.pending.set(id, {resolve, reject, onProgress, timer}); this.worker.postMessage({id, type, ...data}, data.buffer ? [data.buffer] : []); });
 }
 stop(error = new DOMException('转换已取消', 'AbortError')) { this.worker?.terminate(); this.worker = null; for (const p of this.pending.values()) { clearTimeout(p.timer); p.reject(error); } this.pending.clear(); }
}
const bridge = new Bridge();
$('startButton').addEventListener('click', async () => {
 if (running) return;
 if (outputDirectory) {
  try { if (await outputDirectory.requestPermission({mode: 'readwrite'}) !== 'granted') { toast('保存目录未授权，请重新选择，或改用下载保存'); return; } }
  catch (e) { toast('保存目录不可用，请在目录设置中重新选择'); return; }
 }
 const todo = tracks.filter(t => t.ext !== 'lrc' && t.state !== 'done');
 if (!todo.length) return;
 const token = ++runId; running = true; render(); $('runStatus').textContent = '加载本地转码组件…';
 const used = new Set(tracks.filter(t => t.outputName).map(t => t.outputName.toLowerCase()));
 let done = 0, failed = 0;
 try {
  await bridge.request('load'); if (token !== runId) return;
  for (let i = 0; i < todo.length; i++) {
   if (token !== runId) break;
   const track = todo[i]; activeTrack = track; track.state = 'converting'; track.message = '读取文件…'; render(); $('runStatus').textContent = '正在处理 ' + (i + 1) + ' / ' + todo.length;
   try {
    const buffer = await track.file.arrayBuffer(); if (token !== runId) throw new DOMException('转换已取消', 'AbortError');
    const result = await bridge.request('convert', {buffer, extension: track.ext, bitrate: Number(preferences.bitrate)}, (value, label) => progress(track, value, label, i, todo.length));
    if (token !== runId) throw new DOMException('转换已取消', 'AbortError');
    const blob = new Blob([result.buffer], {type: 'audio/mpeg'});
    let name = uniqueName(safeStem(track.file.name) + '.mp3', used);
    if (outputDirectory) name = await writeToDirectory(outputDirectory, name, blob, track.lyric, () => token === runId);
    if (token !== runId) throw new DOMException('转换已取消', 'AbortError');
    track.blob = blob; track.url = URL.createObjectURL(blob); track.outputName = name;
    if (track.lyric) { track.lyricBlob = track.lyric; track.lyricUrl = URL.createObjectURL(track.lyric); }
    track.state = 'done'; track.warning = result.warning; track.message = outputDirectory ? '已保存到目录' : '已完成，点击下载'; if (result.warning) track.message += ' · ' + result.warning; done++;
   } catch (e) {
    if (e.name === 'AbortError' || token !== runId) { track.state = 'cancelled'; track.message = '已取消，可重新开始'; break; }
    track.state = 'error'; track.message = '失败：' + e.message; failed++;
   }
   $('overallProgress').value = Math.round((i + 1) / todo.length * 100); $('overallPercent').textContent = $('overallProgress').value + '%'; render();
  }
  if (token === runId) $('runStatus').textContent = '完成 ' + done + ' 首' + (failed ? ' · 失败 ' + failed + ' 首' : '，音乐准备好了。');
 } catch (e) { if (token === runId) { $('runStatus').textContent = '组件未就绪，请重试'; toast(e.message, 9000); } }
 finally { activeTrack = null; running = false; render(); checkOffline(); }
});
$('cancelButton').addEventListener('click', () => { ++runId; bridge.stop(); if (activeTrack) { activeTrack.state = 'cancelled'; activeTrack.message = '已取消，可重新开始'; } $('runStatus').textContent = '已取消，已完成的音乐仍可下载'; });
$('downloadAll').addEventListener('click', async () => {
 const ready = tracks.filter(t => t.blob); if (!ready.length) return;
 if (ready.reduce((n, t) => n + t.blob.size + (t.lyricBlob?.size || 0), 0) > 256 * 1024 * 1024) { toast('打包文件超过 256 MB，请分批下载或直接保存到目录'); return; }
 $('downloadAll').disabled = true;
 try { const files = {}; const used = new Set(); for (const t of ready) { const name = uniqueName(t.outputName, used); files[name] = new Uint8Array(await t.blob.arrayBuffer()); if (t.lyricBlob) files[stem(name) + '.lrc'] = new Uint8Array(await t.lyricBlob.arrayBuffer()); } const zip = fflate.zipSync(files, {level: 0}); const url = URL.createObjectURL(new Blob([zip], {type: 'application/zip'})); download(url, '轻转-MP3.zip'); setTimeout(() => URL.revokeObjectURL(url), 60000); }
 catch (e) { toast('打包失败：' + e.message); } finally { render(); }
});
window.addEventListener('beforeinstallprompt', e => { e.preventDefault(); installPrompt = e; });
window.addEventListener('appinstalled', () => { installPrompt = null; $('installButton').hidden = true; toast('轻转已安装'); });
$('installButton').addEventListener('click', async () => { if (installPrompt) { await installPrompt.prompt(); await installPrompt.userChoice; installPrompt = null; } else { if (/iPad|iPhone|iPod/.test(navigator.userAgent)) $('installInstructions').textContent = '在 Safari 点击“分享”，再选择“添加到主屏幕”。添加后可像 App 一样打开轻转。'; $('installDialog').showModal(); } });
if (matchMedia('(display-mode: standalone)').matches || navigator.standalone) $('installButton').hidden = true;
window.addEventListener('beforeunload', e => { if (running) { e.preventDefault(); e.returnValue = ''; } });
async function checkOffline() {
 try { const cache = await caches.open(ENGINE_CACHE); const all = await Promise.all(ENGINE_FILES.map(file => caches.match(new URL(file, location.href).href))); const ready = all.every(Boolean); $('offlineStatus').textContent = ready ? '离线转码组件已就绪。安装后也可断网使用。' : navigator.onLine ? '首次需下载约 31 MB 转码组件。' : '当前离线，转码组件尚未缓存。请联网后准备。'; $('prepareOffline').disabled = ready; $('prepareOffline').textContent = ready ? '离线转码已就绪 ✓' : '准备离线转码 ↗'; } catch { $('offlineStatus').textContent = '当前浏览器无法保存离线组件，联网时仍可转换。'; }
}
$('prepareOffline').addEventListener('click', async () => {
 $('prepareOffline').disabled = true;
 try { const cache = await caches.open(ENGINE_CACHE);
  for (const file of ENGINE_FILES) { const url = new URL(file, location.href).href; if (await cache.match(url)) continue; $('offlineStatus').textContent = '正在缓存离线组件，请保持页面打开…'; const response = await fetch(url); if (!response.ok) throw new Error('组件下载失败，请稍后重试'); const reader = response.body?.getReader(); if (!reader) { await cache.put(url, response); continue; } const size = Number(response.headers.get('Content-Length')), chunks = []; let received = 0; while (true) { const {value, done} = await reader.read(); if (done) break; chunks.push(value); received += value.length; $('offlineStatus').textContent = '准备离线组件 ' + (received / 1024 / 1024).toFixed(1) + ' MB' + (size ? ' / ' + (size / 1024 / 1024).toFixed(1) + ' MB' : ''); } const headers = new Headers(response.headers); headers.delete('content-encoding'); headers.set('content-length', String(received)); await cache.put(url, new Response(new Blob(chunks), {status: 200, headers})); }
  toast('离线转码组件已准备好');
 } catch (e) { toast(e.message || '无法缓存离线组件'); }
 finally { await checkOffline(); }
});
if ('serviceWorker' in navigator) navigator.serviceWorker.register('./sw.js').then(() => navigator.serviceWorker.ready).then(() => checkOffline()).catch(() => { $('offlineStatus').textContent = '离线模式未启用；请使用 HTTPS 或 localhost 打开。'; });
else $('offlineStatus').textContent = '当前浏览器不支持离线安装，联网转换仍可使用。';
window.addEventListener('online', checkOffline); window.addEventListener('offline', checkOffline);
render(); await handlesReady;
