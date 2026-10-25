(function (scope) {
 'use strict';
 function convert(core, source, bitrate, onProgress) {
  const name = 'input.' + source.format, files = [name, 'cover', 'result.mp3'];
  let logs = [];
  core.setLogger(({message}) => { logs.push(message); if (logs.length > 20) logs.shift(); });
  core.setProgress(({progress}) => { if (Number.isFinite(progress)) onProgress(Math.max(0, Math.min(0.99, progress))); });
  try {
   core.FS.writeFile(name, source.audio);
   const args = ['-hide_banner', '-loglevel', 'error', '-nostdin', '-y', '-i', name];
   if (source.cover) { core.FS.writeFile('cover', source.cover); args.push('-i', 'cover'); }
   args.push('-map', '0:a:0');
   if (source.cover) args.push('-map', '1:v:0', '-c:v', 'copy', '-disposition:v', 'attached_pic');
   else args.push('-map', '0:v?', '-c:v', 'copy');
   args.push('-map_metadata', '0', '-id3v2_version', '3');
   if (source.format === 'mp3') args.push('-c:a', 'copy');
   else args.push('-c:a', 'libmp3lame', '-b:a', String(bitrate) + 'k');
   const meta = source.metadata;
   for (const [key, value] of [['title', meta.musicName], ['album', meta.album], ['artist', Array.isArray(meta.artist) ? meta.artist.map(a => Array.isArray(a) ? a[0] : '').filter(Boolean).join(' / ') : '']]) {
    if (value) args.push('-metadata', key + '=' + String(value).slice(0, 10000));
   }
   args.push('result.mp3');
   core.setTimeout(-1);
   core.exec(...args);
   if (core.ret !== 0) throw new Error(logs.join('\n').slice(-900) || '转码失败，文件可能损坏');
   const result = core.FS.readFile('result.mp3').slice();
   if (result.length < 100) throw new Error('转换结果为空');
   onProgress(1);
   return result;
  } finally { for (const f of files) try { core.FS.unlink(f); } catch {} core.reset(); }
 }
 scope.QZTranscode = {convert};
 if (typeof module !== 'undefined' && module.exports) module.exports = {convert};
})(typeof self !== 'undefined' ? self : globalThis);
