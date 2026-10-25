// Serialize workspaces across tabs and installed windows in this browser profile.
export async function acquireWorkspace(locks, onWaiting = () => {}) {
 if (!locks) return;
 return new Promise((resolve, reject) => {
  const hold = async () => { resolve(); await new Promise(() => {}); };
  locks.request('qingzhuan-music-workspace-v1', {ifAvailable: true}, async lock => {
   if (lock) return hold();
   onWaiting();
   locks.request('qingzhuan-music-workspace-v1', async () => hold()).catch(reject);
  }).catch(reject);
 });
}
