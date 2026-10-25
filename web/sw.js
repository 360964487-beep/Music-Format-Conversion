const SHELL = 'qz-pwa-shell-1.0.1';
const ENGINE = 'qz-engine-0.12.10';
const ASSETS = ['./', './index.html', './styles.css', './app.mjs', './files.mjs', './ncm.js', './transcode.js', './converter.js', './manifest.webmanifest', './icons/icon.svg', './icons/icon-192.png', './icons/icon-512.png', './vendor/aes.js', './vendor/fflate.js'];
self.addEventListener('install', event => { event.waitUntil(caches.open(SHELL).then(cache => cache.addAll(ASSETS)).then(() => self.skipWaiting())); });
self.addEventListener('activate', event => { event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(k => k.startsWith('qz-pwa-shell-') && k !== SHELL).map(k => caches.delete(k)))).then(() => self.clients.claim())); });
self.addEventListener('fetch', event => {
 const request = event.request, url = new URL(request.url);
 if (request.method !== 'GET' || url.origin !== self.location.origin || !url.pathname.startsWith(new URL(self.registration.scope).pathname)) return;
 event.respondWith((async () => {
  const cached = await caches.match(request, {ignoreSearch: request.mode === 'navigate'});
  if (cached) return cached;
  try {
   const response = await fetch(request);
   if (response.ok && /\/vendor\/(ffmpeg-core\.(js|wasm)|aes\.js)$/.test(url.pathname)) {
    const cache = await caches.open(ENGINE); await cache.put(request, response.clone());
   }
   return response;
  } catch (e) {
   if (request.mode === 'navigate') { const cache = await caches.open(SHELL); return await cache.match('./index.html') || Response.error(); }
   throw e;
  }
 })());
});
