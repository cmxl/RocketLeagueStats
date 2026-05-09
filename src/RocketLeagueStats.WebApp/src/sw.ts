/// <reference lib="webworker" />
/* eslint-disable no-restricted-globals */
import { precacheAndRoute, matchPrecache, type PrecacheEntry } from 'workbox-precaching';
import { NavigationRoute, registerRoute, setCatchHandler } from 'workbox-routing';
import { CacheFirst, NetworkFirst } from 'workbox-strategies';
import { ExpirationPlugin } from 'workbox-expiration';
import { CacheableResponsePlugin } from 'workbox-cacheable-response';
import type { ClientMessage } from './sw-protocol';

declare const self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<PrecacheEntry | string>;
};

const CACHE_NAMES = {
  api: 'rls-api',
  images: 'rls-images',
  fonts: 'rls-fonts',
} as const;

const EXPIRATION = {
  api:    { maxEntries: 200, maxAgeSeconds: 24 * 60 * 60 },
  images: { maxEntries: 60,  maxAgeSeconds: 30 * 24 * 60 * 60 },
  fonts:  { maxEntries: 30,  maxAgeSeconds: 365 * 24 * 60 * 60 },
} as const;

// App shell from the build manifest (filled in at build time by injectManifest).
precacheAndRoute(self.__WB_MANIFEST);

// Navigation -> serve the precached SPA shell. Angular's router resolves the
// actual path client-side, so deep links (/live, /history, /recap/:id, ...)
// all rehydrate from this single document.
registerRoute(
  new NavigationRoute(async () => (await matchPrecache('/index.html')) ?? Response.error()),
);

// Stats GETs: NetworkFirst with a short timeout. Live numbers must win when
// the network is healthy; cache exists purely as an offline fallback. SWR
// would happily paint yesterday's standings before revalidating, which is
// actively misleading for a live-stats UI.
//
// /api/settings is excluded: writes need read-after-write consistency, and a
// stale value served during a brief offline blip would lie about server
// state. POST/PUT/DELETE are filtered out by request.method, so they always
// hit the network.
//
// /hub/* (SignalR negotiate + WebSocket upgrade) and /health are deliberately
// not registered as routes - Workbox only intercepts URLs whose handlers
// match, so these fall through to the platform's default fetch path.
registerRoute(
  ({ url, request }) =>
    url.pathname.startsWith('/api/') &&
    !url.pathname.startsWith('/api/settings') &&
    request.method === 'GET',
  new NetworkFirst({
    cacheName: CACHE_NAMES.api,
    networkTimeoutSeconds: 3,
    plugins: [
      new CacheableResponsePlugin({ statuses: [200] }),
      new ExpirationPlugin(EXPIRATION.api),
    ],
  }),
);

// Images: CacheFirst with LRU. Status 0 lets opaque cross-origin responses
// (e.g. fonts.googleapis fallbacks, future CDN-hosted assets) cache too.
registerRoute(
  ({ request }) => request.destination === 'image',
  new CacheFirst({
    cacheName: CACHE_NAMES.images,
    plugins: [
      new CacheableResponsePlugin({ statuses: [0, 200] }),
      new ExpirationPlugin(EXPIRATION.images),
    ],
  }),
);

// Fonts: long-lived, immutable. Google Fonts (Bebas Neue / Rajdhani / Inter)
// land here on first load and stay for a year unless evicted by LRU.
registerRoute(
  ({ request }) => request.destination === 'font',
  new CacheFirst({
    cacheName: CACHE_NAMES.fonts,
    plugins: [new ExpirationPlugin(EXPIRATION.fonts)],
  }),
);

// Catch handler: only navigations get the offline shell. Other failed
// requests (API GET miss with no cache, asset 404, etc.) surface their
// real error to the caller instead of being masked by a fake 200.
setCatchHandler(async ({ request }) => {
  if (request.destination === 'document') {
    return (await matchPrecache('/offline.html')) ?? Response.error();
  }
  return Response.error();
});

// SKIP_WAITING is the only client->SW message. AppUpdateClient posts it when
// the user accepts an update; the SW activates and fires controllerchange,
// after which the client reloads. Do NOT call skipWaiting() from `install` -
// that would short-circuit the "update available" detection by skipping the
// `waiting` state entirely.
self.addEventListener('message', (event) => {
  const data = event.data as ClientMessage | undefined;
  if (data?.type === 'SKIP_WAITING') self.skipWaiting();
});

// clients.claim() is required: without it, the activated SW won't take
// control of the current page until the next navigation, which feels broken
// even though it's working as designed.
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));
