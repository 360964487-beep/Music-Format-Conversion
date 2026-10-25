export const MAX_BYTES = 128 * 1024 * 1024;
export const MAX_FILES = 200;
export function extension(name) { return name.split('.').at(-1).toLowerCase(); }
export function stem(name) { return name.replace(/\.[^.]*$/, ''); }
export function safeStem(name) { let value = Array.from(stem(name).replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_').replace(/[. ]+$/, '')).slice(0, 150).join('') || '音乐'; if (/^(con|prn|aux|nul|com[0-9]|lpt[0-9])$/i.test(value)) value = '_' + value; return value; }
export function displaySize(bytes) { return bytes < 1024 * 1024 ? Math.max(1, Math.round(bytes / 1024)) + ' KB' : (bytes / 1024 / 1024).toFixed(1) + ' MB'; }
export function uniqueName(name, used) {
 let result = name, count = 1;
 while (used.has(result.toLowerCase())) result = stem(name) + ' (' + count++ + ').' + extension(name);
 used.add(result.toLowerCase()); return result;
}
export function pairLyrics(tracks) {
 const lyrics = tracks.filter(t => t.ext === 'lrc');
 for (const track of tracks.filter(t => t.ext !== 'lrc')) {
  if (track.manualLyric) { track.lyric = track.manualLyric; continue; }
  const basename = stem(track.file.name).toLowerCase();
  const parent = track.path.slice(0, -track.file.name.length).toLowerCase();
  const candidates = lyrics.filter(l => stem(l.file.name).toLowerCase() === basename);
  const local = candidates.filter(l => l.path.slice(0, -l.file.name.length).toLowerCase() === parent);
  track.lyric = (local.length === 1 ? local[0] : candidates.length === 1 ? candidates[0] : null)?.file || null;
  track.lyricAmbiguous = !track.lyric && candidates.length > 1;
 }
}
export async function collectDirectory(handle, prefix = '', limit = MAX_FILES) {
 const result = [];
 for await (const entry of handle.values()) {
  if (result.length >= limit) break;
  if (entry.kind === 'directory') result.push(...await collectDirectory(entry, prefix + entry.name + '/', limit - result.length));
  else if (['ncm', 'flac', 'mp3', 'lrc'].includes(extension(entry.name))) { const file = await entry.getFile(); result.push({file, path: prefix + file.name}); }
 }
 return result;
}
export async function writeToDirectory(handle, filename, audio, lyric, isCurrent = () => true) {
 let chosen = filename, suffix = 1, created = [], closed = [];
 async function exists(name) { try { await handle.getFileHandle(name); return true; } catch (e) { if (e.name === 'NotFoundError') return false; if (e.name === 'TypeMismatchError') return true; throw e; } }
 while (await exists(chosen) || await exists(stem(chosen) + '.lrc')) chosen = stem(filename) + ' (' + suffix++ + ').mp3';
 if (!isCurrent()) throw new DOMException('转换已取消', 'AbortError');
 try {
  for (const [name, blob] of [[chosen, audio], ...(lyric ? [[stem(chosen) + '.lrc', lyric]] : [])]) {
   if (!isCurrent()) throw new DOMException('转换已取消', 'AbortError');
   const file = await handle.getFileHandle(name, {create: true}); created.push(name);
   const writable = await file.createWritable();
   try { await writable.write(blob); if (!isCurrent()) throw new DOMException('转换已取消', 'AbortError'); await writable.close(); closed.push(name); }
   catch (e) { try { await writable.abort(); } catch {} throw e; }
  }
  return chosen;
 } catch (error) {
  // Only files reserved by this operation are removed; completed earlier tracks are untouched.
  for (const name of created) try { await handle.removeEntry(name); } catch {}
  throw error;
 }
}
