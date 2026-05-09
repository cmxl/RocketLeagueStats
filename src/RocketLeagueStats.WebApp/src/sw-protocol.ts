// Shared protocol types for the SW <-> client message channel.
// Imported by both src/sw.ts (the worker) and the client class under
// src/app/core/offline/. Single canonical source - changing a message
// shape produces a compile error on both sides until updated.

/** Messages the host page sends TO the service worker. */
export type ClientMessage = { type: 'SKIP_WAITING' };
