/* Integration tests execute the actual WebAssembly core; no mock audio codec. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const {pathToFileURL} = require('node:url');
const root = path.resolve(__dirname, '../web');
function encrypt(data,key) { const c=crypto.createCipheriv('aes-128-ecb',Buffer.from(key),null);return Buffer.concat([c.update(data),c.final()]); }
function ncmFixture(audio,metadata=true,cover=Buffer.alloc(0)) {
 const key=Buffer.from('test-independent-key-1234');
 const encrypted=encrypt(Buffer.concat([Buffer.from('neteasecloudmusic'),key]),'hzHRAmso5kInbaxW').map(v=>v^100);
 const meta=metadata?Buffer.from("163 key(Don't modify):"+encrypt(Buffer.from('music:'+JSON.stringify({musicName:'PWA测试',album:'本地专辑',artist:[['测试歌手',1]]})),"#14ljk_!\\]&0U<'(").toString('base64')).map(v=>v^99):Buffer.alloc(0);
 const box=Array.from({length:256},(_,i)=>i);let j=0;
 for(let i=0;i<256;i++){j=(j+box[i]+key[i%key.length])%256;[box[i],box[j]]=[box[j],box[i]];}
 const data=Buffer.from(audio);for(let i=0;i<data.length;i++){const a=(i+1)%256;data[i]^=box[(box[a]+box[(box[a]+a)%256])%256];}
 const u=n=>{const b=Buffer.alloc(4);b.writeUInt32LE(n);return b;};
 return Buffer.concat([Buffer.from('CTENFDAM'),Buffer.alloc(2),u(encrypted.length),encrypted,u(meta.length),meta,Buffer.alloc(5),u(cover.length+19),u(cover.length),cover,Buffer.alloc(19),data]);
}
function wav() {
 const count=22050,rate=44100,buffer=Buffer.alloc(44+count*2);buffer.write('RIFF');buffer.writeUInt32LE(buffer.length-8,4);buffer.write('WAVEfmt ',8);buffer.writeUInt32LE(16,16);buffer.writeUInt16LE(1,20);buffer.writeUInt16LE(1,22);buffer.writeUInt32LE(rate,24);buffer.writeUInt32LE(rate*2,28);buffer.writeUInt16LE(2,32);buffer.writeUInt16LE(16,34);buffer.write('data',36);buffer.writeUInt32LE(count*2,40);for(let i=0;i<count;i++)buffer.writeInt16LE(Math.round(Math.sin(i*2*Math.PI*440/rate)*12000),44+i*2);return buffer;
}
function clone(buffer){return Uint8Array.from(buffer).buffer;}
async function main(){
 global.self={location:{href:'http://localhost/vendor/ffmpeg-core.js'}};
 self.aesjs=require(path.join(root,'vendor/aes.js'));
 const parser=require(path.join(root,'ncm.js')),transcoder=require(path.join(root,'transcode.js'));
 const factory=require(path.join(root,'vendor/ffmpeg-core.js'));
 const core=await factory({wasmBinary:fs.readFileSync(path.join(root,'vendor/ffmpeg-core.wasm'))});
 core.setLogger(()=>{});core.setProgress(()=>{});
 function exec(args){core.exec(...args);assert.equal(core.ret,0);core.reset();}
 core.FS.writeFile('tone.wav',wav());exec(['-i','tone.wav','tone.flac']);exec(['-i','tone.wav','-c:a','libmp3lame','-b:a','128k','tone.mp3']);
 const flac=core.FS.readFile('tone.flac').slice(),mp3=core.FS.readFile('tone.mp3').slice();
 const cover=fs.readFileSync(path.join(root,'icons/icon-192.png'));
 let ncmOutput;
 for(const [extension,input] of [['flac',flac],['mp3',mp3],['ncm',ncmFixture(mp3,true,cover)],['ncm',ncmFixture(flac)],['ncm',ncmFixture(mp3,false)]]){
  const source=parser.prepare(clone(input),extension);if(extension==='ncm')assert.ok(source.format==='flac'||source.format==='mp3');
  let progress=0;const output=transcoder.convert(core,source,192,p=>progress=p);assert.ok(output.length>100);assert.equal(progress,1);assert.ok(!core.FS.readdir('/').includes('result.mp3'));if(source.metadata.musicName)ncmOutput=output;
 }
 console.log('PASS actual WASM conversion: FLAC, MP3, NCM/MP3, NCM/FLAC, missing metadata; output cleanup');
 const decoded=parser.prepare(clone(ncmFixture(mp3)), 'ncm');assert.deepEqual(decoded.audio,mp3);assert.equal(decoded.metadata.musicName,'PWA测试');assert.throws(()=>parser.decodeNcm(new Uint8Array([1,2,3]).buffer));
 const malformed=ncmFixture(mp3);malformed.writeUInt32LE(0xffffffff,10);assert.throws(()=>parser.decodeNcm(clone(malformed)));
 console.log('PASS NCM byte-exact audio extraction, metadata, malformed header/length rejection');
 core.FS.writeFile('verified.mp3',ncmOutput);const logs=[];core.setLogger(({message})=>logs.push(message));exec(['-hide_banner','-loglevel','info','-i','verified.mp3','-f','null','-']);assert.ok(logs.join('\n').includes('PWA测试'));assert.ok(logs.join('\n').includes('测试歌手'));
 const helpers=await import(pathToFileURL(path.join(root,'files.mjs')));
 const a={file:{name:'同名.flac'},path:'A/同名.flac',ext:'flac'},la={file:{name:'同名.lrc'},path:'A/同名.lrc',ext:'lrc'},lb={file:{name:'同名.lrc'},path:'B/同名.lrc',ext:'lrc'};
 helpers.pairLyrics([a,la,lb]);assert.equal(a.lyric,la.file);a.path='C/同名.flac';helpers.pairLyrics([a,la,lb]);assert.equal(a.lyric,null);assert.equal(a.lyricAmbiguous,true);
 const used=new Set(['同名.mp3']);assert.equal(helpers.uniqueName('同名.mp3',used),'同名 (1).mp3');assert.equal(helpers.safeStem('CON.mp3'),'_CON');assert.ok(!helpers.safeStem('../abc.mp3').includes('/'));
 console.log('PASS lyric matching by directory, ambiguous matches, safe names and numbering');
 // A filesystem handle stub exercises write transactions, collision protection, and cancellation rollback.
 const files=new Map([['track.mp3',new Blob(['original'])]]);
 const dir={async getFileHandle(name,{create=false}={}){if(!files.has(name)){if(!create)throw new DOMException('missing','NotFoundError');files.set(name,new Blob());}return{async createWritable(){let data;return{async write(value){data=value;},async close(){files.set(name,data);},async abort(){}}}};},async removeEntry(name){files.delete(name);}};
 assert.equal(await helpers.writeToDirectory(dir,'track.mp3',new Blob(['new']),new Blob(['lyric'])),'track (1).mp3');assert.equal(await files.get('track.mp3').text(),'original');assert.ok(files.has('track (1).lrc'));
 let checks=0;await assert.rejects(helpers.writeToDirectory(dir,'cancel.mp3',new Blob(['new']),null,()=>++checks<3));assert.ok(!files.has('cancel.mp3'));assert.ok(files.has('track.mp3'));
 console.log('PASS directory collision protection, audio/lyric sidecars, cancellation rollback');
 const ziplib=require(path.join(root,'vendor/fflate.js'));const archive=ziplib.zipSync({'中文.mp3':new Uint8Array([1,2]),'中文.lrc':new TextEncoder().encode('[00:00]歌词')},{level:0});const unzipped=ziplib.unzipSync(archive);assert.equal(Object.keys(unzipped).length,2);assert.equal(new TextDecoder().decode(unzipped['中文.lrc']),'[00:00]歌词');
 await import('./instance.test.mjs');
 await serviceWorkerTest();
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'manifest.webmanifest'),'utf8').replace(/^\uFEFF/,''));assert.equal(manifest.display,'standalone');assert.equal(manifest.id,'/Music-Format-Conversion/');assert.equal(manifest.launch_handler.client_mode,'focus-existing');assert.equal(manifest.scope,'./');for(const icon of manifest.icons)assert.ok(fs.existsSync(path.resolve(root,icon.src)));
 console.log('PASS PWA manifest, standalone installation assets, ZIP Unicode filenames');
}
async function serviceWorkerTest(){
 const listeners={},stores=new Map(),scope='https://example.test/Music-Format-Conversion/';let offline=false;
 const key=r=>typeof r==='string'?new URL(r,scope).href:r.url;
 const network=async r=>{if(offline)throw Error('offline');const url=new URL(key(r));let name=url.pathname.slice(new URL(scope).pathname.length)||'index.html';if(!fs.existsSync(path.join(root,name)))return new Response('missing',{status:404});return new Response(fs.readFileSync(path.join(root,name)));};
 const cacheApi={async open(name){if(!stores.has(name))stores.set(name,new Map());const map=stores.get(name);return{async addAll(items){for(const item of items)map.set(key(item),await network(item));},async match(r){return map.get(key(r))?.clone();},async put(r,response){map.set(key(r),response.clone());}};},async keys(){return [...stores.keys()];},async delete(k){return stores.delete(k);},async match(r){for(const map of stores.values()){const result=map.get(key(r));if(result)return result.clone();}}};
 const context={self:{location:{origin:'https://example.test'},registration:{scope},addEventListener:(n,fn)=>listeners[n]=fn,skipWaiting:async()=>{},clients:{claim:async()=>{}}},URL,Request,Response,caches:cacheApi,fetch:network};vm.runInNewContext(fs.readFileSync(path.join(root,'sw.js'),'utf8'),context);
 let pending;listeners.install({waitUntil:p=>pending=p});await pending;listeners.activate({waitUntil:p=>pending=p});await pending;
 async function request(file,mode='cors'){let result;listeners.fetch({request:{url:new URL(file,scope).href,method:'GET',mode},respondWith:p=>result=p});return result;}
 const wasm=await request('vendor/ffmpeg-core.wasm');assert.equal(wasm.status,200);offline=true;
 assert.ok((await (await request('index.html','navigate')).text()).includes('轻转'));
 assert.equal((await (await request('vendor/ffmpeg-core.wasm')).arrayBuffer()).byteLength,fs.statSync(path.join(root,'vendor/ffmpeg-core.wasm')).size);
 assert.equal((await request('app.mjs')).status,200);
 console.log('PASS service-worker shell install, first-use engine caching, navigation and WASM retrieval with network unavailable');
}
main().catch(e=>{console.error(e);process.exitCode=1;});

