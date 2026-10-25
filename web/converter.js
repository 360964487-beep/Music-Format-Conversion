'use strict';
importScripts('./vendor/aes.js', './ncm.js', './transcode.js', './vendor/ffmpeg-core.js');
let core;
async function load() {
 if (!core) {
  const url = new URL('./vendor/ffmpeg-core.js', self.location.href).href;
  core = await createFFmpegCore({mainScriptUrlOrBlob: url + '#' + btoa(JSON.stringify({wasmURL: url.replace(/\.js$/, '.wasm')}))});
 }
}
self.onmessage = async ({data}) => {
 const {id, type} = data;
 try {
  await load();
  if (type === 'load') { self.postMessage({id, type: 'ready'}); return; }
  if (data.buffer.byteLength > 128 * 1024 * 1024) throw new Error('单个文件最大支持 128 MB，请用桌面版处理更大的文件');
  self.postMessage({id, type: 'progress', value: 0.04, label: '解析音频'});
  const source = QZCore.prepare(data.buffer, data.extension);
  const output = QZTranscode.convert(core, source, data.bitrate, progress => self.postMessage({id, type: 'progress', value: 0.08 + progress * 0.92, label: source.format === 'mp3' ? '提取 MP3' : '转换 MP3'}));
  self.postMessage({id, type: 'result', buffer: output.buffer, warning: source.warning}, [output.buffer]);
 } catch (error) { self.postMessage({id, type: 'error', message: error.message || '处理失败'}); }
};
