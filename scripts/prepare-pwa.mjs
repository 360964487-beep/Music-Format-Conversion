import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const root=new URL('../',import.meta.url);
const dependencies=JSON.parse(await readFile(new URL('pwa-dependencies.json',import.meta.url),'utf8'));
await mkdir(new URL('web/vendor/',root),{recursive:true});
for(const dep of dependencies){
 const target=new URL('web/vendor/'+dep.file,root);
 let bytes;
 try { const current=await readFile(target);if(createHash('sha256').update(current).digest('hex')===dep.sha256)bytes=current; } catch {}
 if(!bytes){
  const response=await fetch(dep.url,{signal:AbortSignal.timeout(180000)});
  if(!response.ok)throw new Error('Download failed: '+dep.file+' HTTP '+response.status);
  bytes=Buffer.from(await response.arrayBuffer());
  if(createHash('sha256').update(bytes).digest('hex')!==dep.sha256)throw new Error('SHA-256 mismatch: '+dep.file);
  await writeFile(target,bytes);
 }
 console.log('Verified '+dep.file+' ('+bytes.length+' bytes)');
}
