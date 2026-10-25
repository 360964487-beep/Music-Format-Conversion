/* NCM decoding is performed locally. No account or network access is used. */
(function (scope) {
 'use strict';
 const text = new TextDecoder();
 const encoder = new TextEncoder();
 function type(bytes) {
  if (bytes.length < 4) throw new Error('音频文件为空或不完整');
  if (text.decode(bytes.subarray(0, 4)) === 'fLaC') return 'flac';
  if (text.decode(bytes.subarray(0, 3)) === 'ID3' || bytes[0] === 255 && (bytes[1] & 224) === 224) return 'mp3';
  throw new Error('无法识别音频内容，文件可能损坏或格式不受支持');
 }
 function decodeNcm(buffer) {
  const bytes = new Uint8Array(buffer), view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let offset = 0, metadata = {}, cover = null, warning = '';
  function take(size) {
   if (!Number.isInteger(size) || size < 0 || offset + size > bytes.length) throw new Error('NCM 文件不完整或数据长度异常');
   const result = bytes.subarray(offset, offset + size); offset += size; return result;
  }
  function uint() { if (offset + 4 > bytes.length) throw new Error('NCM 文件头不完整'); const value = view.getUint32(offset, true); offset += 4; return value; }
  function block() { const length = uint(); if (length > 64 * 1024 * 1024) throw new Error('NCM 数据段过大'); return take(length); }
  function decrypt(data, key) {
   const aes = scope.aesjs;
   if (!aes) throw new Error('本地解码组件未加载，请联网后准备离线组件');
   return aes.padding.pkcs7.strip(new aes.ModeOfOperation.ecb(encoder.encode(key)).decrypt(data));
  }
  if (text.decode(take(8)) !== 'CTENFDAM') throw new Error('不是有效的 NCM 文件');
  take(2);
  const encryptedKey = block().map(v => v ^ 0x64);
  const decodedKey = decrypt(encryptedKey, 'hzHRAmso5kInbaxW');
  if (text.decode(decodedKey.subarray(0, 17)) !== 'neteasecloudmusic' || decodedKey.length <= 17) throw new Error('暂不支持此 NCM 密钥版本');
  const key = decodedKey.subarray(17), box = Uint8Array.from({length: 256}, (_, i) => i);
  let last = 0;
  for (let i = 0; i < 256; i++) { last = (last + box[i] + key[i % key.length]) & 255; const v = box[i]; box[i] = box[last]; box[last] = v; }
  const mask = Uint8Array.from({length: 256}, (_, i) => { const a = (i + 1) & 255; return box[(box[a] + box[(box[a] + a) & 255]) & 255]; });
  const meta = block();
  if (meta.length) {
   try {
    const encoded = text.decode(meta.map(v => v ^ 0x63)).slice(22);
    const raw = Uint8Array.from(atob(encoded), char => char.charCodeAt(0));
    const json = text.decode(decrypt(raw, "#14ljk_!\\]&0U<'("));
    if (!json.startsWith('music:')) throw new Error('unknown metadata');
    metadata = JSON.parse(json.slice(6));
   } catch { warning = '歌曲信息未能解析，已保留可用音频'; }
  }
  take(5);
  const frame = uint(), imageLength = uint();
  if (frame < imageLength || frame > 64 * 1024 * 1024) throw new Error('NCM 封面数据异常');
  const image = take(imageLength); take(frame - imageLength);
  if (image.length && (image[0] === 255 && image[1] === 216 || image[0] === 137 && image[1] === 80)) cover = image.slice();
  const audio = bytes.subarray(offset);
  for (let i = 0; i < audio.length; i++) audio[i] ^= mask[i & 255];
  return {audio, format: type(audio), metadata: metadata || {}, cover, warning};
 }
 function prepare(buffer, extension) {
  if (extension.toLowerCase() === 'ncm') return decodeNcm(buffer);
  const audio = new Uint8Array(buffer);
  return {audio, format: type(audio), metadata: {}, cover: null, warning: ''};
 }
 const api = {type, decodeNcm, prepare};
 scope.QZCore = api;
 if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof self !== 'undefined' ? self : globalThis);
