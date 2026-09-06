const CACHE_NAME = 'ctship-pwa-v1';
const CORE_ASSETS = [
  '/offline.html',
  '/manifest.json',
  '/css/dashboard-theme.css',
  '/js/site.js',
  '/images/nedc-logo-transparent.png',
  '/img/nedc-logo-192.png',
  '/img/nedc-logo-512.png',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/bootstrap-icons/font/bootstrap-icons.min.css',
  '/lib/bootstrap/dist/js/bootstrap.bundle.min.js'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(CORE_ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys
        .filter(key => key !== CACHE_NAME)
        .map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const { request } = event;

  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(() => caches.match('/offline.html'))
    );
    return;
  }

  if (!isStaticAsset(url.pathname)) {
    return;
  }

  event.respondWith(
    caches.match(request).then(cachedResponse => {
      const fetchPromise = fetch(request)
        .then(networkResponse => {
          if (networkResponse && networkResponse.ok) {
            const responseClone = networkResponse.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(request, responseClone));
          }
          return networkResponse;
        })
        .catch(() => cachedResponse);

      return cachedResponse || fetchPromise;
    })
  );
});

function isStaticAsset(pathname) {
  return pathname === '/manifest.json'
    || pathname === '/offline.html'
    || pathname.startsWith('/css/')
    || pathname.startsWith('/js/')
    || pathname.startsWith('/lib/')
    || pathname.startsWith('/img/')
    || pathname.startsWith('/images/');
}
