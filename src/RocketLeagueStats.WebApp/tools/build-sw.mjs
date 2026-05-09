// Post-build script: bundles src/sw.ts and injects the precache manifest.
// Runs AFTER `ng build` finishes. Wired via package.json:
//   "build": "ng build && node ./tools/build-sw.mjs"
//
// Why this exists: workbox-webpack-plugin doesn't work with Angular 17+'s
// esbuild builder. Instead we run two stages here as a separate process -
// (1) esbuild bundles the SW source, (2) workbox-build's injectManifest()
// rewrites the file to embed the precache manifest.
//
// Configuration:
//   WB_APP_NAME - Angular project name (the folder created under dist/).
//                 Defaults to "RocketLeagueStats.WebApp" for this repo.
//                 Override only if angular.json's project name changes.

import { build as esbuildBuild } from 'esbuild';
import { injectManifest } from 'workbox-build';
import { existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// tools/build-sw.mjs -> project root (src/RocketLeagueStats.WebApp) is one up.
const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = resolve(SCRIPT_DIR, '..');

const APP_NAME = process.env.WB_APP_NAME ?? 'RocketLeagueStats.WebApp';
const DIST = resolve(PROJECT_ROOT, `dist/${APP_NAME}/browser`);
const SW_SRC = resolve(PROJECT_ROOT, 'src/sw.ts');
const SW_OUT = resolve(DIST, 'sw.js');

// 5 MB ceiling per file. Anything bigger almost certainly belongs in a
// network-fetched cache, not the precache. Bumps the entire bundle into
// every user's storage on first install.
const MAX_PRECACHE_BYTES = 5 * 1024 * 1024;

if (!existsSync(DIST)) {
  console.error(`Workbox build: ${DIST} does not exist.`);
  console.error(`Did 'ng build' run? Is WB_APP_NAME correct (currently '${APP_NAME}')?`);
  process.exit(1);
}
if (!existsSync(SW_SRC)) {
  console.error(`Workbox build: ${SW_SRC} does not exist.`);
  process.exit(1);
}

// Stage 1 - esbuild bundles sw.ts to a single iife. target: es2022 matches
// the PWA-viable iOS Safari floor (16.4+); Workbox 7 emits ES2017 internally
// so dropping the target lower would still work.
//
// The NODE_ENV define is required, not optional: Workbox's modules read
// process.env.NODE_ENV at module scope to strip dev logging. Without it,
// esbuild leaves `process.env.NODE_ENV` as a free identifier and the SW
// throws ReferenceError on first dispatch.
await esbuildBuild({
  entryPoints: [SW_SRC],
  outfile: SW_OUT,
  bundle: true,
  format: 'iife',
  target: 'es2022',
  platform: 'browser',
  minify: true,
  sourcemap: true,
  define: { 'process.env.NODE_ENV': '"production"' },
  logLevel: 'info',
});

// Stage 2 - injectManifest reads swSrc fully before writing, so swSrc===swDest
// is safe (in-place rewrite of the file Stage 1 produced).
const result = await injectManifest({
  swSrc: SW_OUT,
  swDest: SW_OUT,
  globDirectory: DIST,
  globPatterns: ['**/*.{js,css,html,svg,png,webp,avif,woff2,json,webmanifest,ico}'],
  globIgnores: ['**/sw.js', '**/sw.js.map'],
  maximumFileSizeToCacheInBytes: MAX_PRECACHE_BYTES,
});

console.log(
  `Workbox: ${result.count} files precached (${(result.size / 1024 / 1024).toFixed(2)} MB total).`,
);
if (result.warnings.length > 0) console.warn(result.warnings.join('\n'));
