import assert from 'node:assert/strict';
import {acquireWorkspace} from '../web/instance.mjs';
let active = false;
const pending = [];
const locks = {
 request(name, options, callback) {
  if (typeof options === 'function') { callback = options; options = {}; }
  assert.equal(name, 'qingzhuan-music-workspace-v1');
  if (active && options.ifAvailable) return Promise.resolve(callback(null));
  if (active) { pending.push(callback); return new Promise(() => {}); }
  active = true;
  return Promise.resolve(callback({name}));
 },
 releaseOwner() { active = false; const next = pending.shift(); if (next) { active = true; next({}); } }
};
let waiting = 0, opened = 0;
await acquireWorkspace(locks).then(() => opened++);
const second = acquireWorkspace(locks, () => waiting++).then(() => opened++);
const third = acquireWorkspace(locks, () => waiting++).then(() => opened++);
await Promise.resolve();
assert.equal(opened, 1); assert.equal(waiting, 2);
locks.releaseOwner(); await second; assert.equal(opened, 2); assert.equal(pending.length, 1);
locks.releaseOwner(); await third; assert.equal(opened, 3);
await acquireWorkspace(undefined);
console.log('PASS exclusive workspace, duplicate waiting, ordered takeover and unsupported-browser fallback');
