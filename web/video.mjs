const local = 'http://127.0.0.1:18746';
const apiBase = location.hostname === '360964487-beep.github.io' ? local : location.origin;
const hosted = location.protocol === 'https:' && apiBase === location.origin;
const dialog = document.querySelector('#videoDialog');
const form = document.querySelector('#videoForm');
const start = document.querySelector('#startVideo');
const status = document.querySelector('#videoStatus');
const save = document.querySelector('#saveVideo');
const link = document.querySelector('#videoLink');
const cookies = document.querySelector('#videoCookies');

if (apiBase === location.origin) document.querySelector('#videoSetup').hidden = true;
if (hosted) cookies.closest('details').hidden = true;
document.querySelector('#openVideo').addEventListener('click', () => dialog.showModal());

async function api(path, options) {
 const response = await fetch(apiBase + path, {...options, signal: AbortSignal.timeout(15000)});
 const result = await response.json();
 if (!response.ok) throw new Error(result.error || '下载服务返回错误，请重试。');
 return result;
}

form.addEventListener('submit', async event => {
 event.preventDefault();
 if (start.disabled) return;
 const file = hosted ? null : cookies.files[0];
 if (file?.size > 80000) {
  status.textContent = 'Cookie 文件超过 80 KB，请选择仅含抖音站点的文件。';
  return;
 }
 start.disabled = true;
 save.hidden = true;
 status.textContent = '正在连接下载服务…';
 try {
  const result = await api('/jobs', {
   method: 'POST',
   headers: {'Content-Type': 'application/json'},
   body: JSON.stringify({text: link.value.trim(), cookies: file ? await file.text() : ''})
  });
  status.textContent = '正在解析并下载视频，请保持页面打开…';
  const deadline = Date.now() + 660000;
  while (Date.now() < deadline) {
   await new Promise(resolve => setTimeout(resolve, 1500));
   const job = await api('/jobs/' + result.id);
   if (job.status === 'error') throw new Error(job.error);
   if (job.status === 'ready') {
    save.href = apiBase + '/jobs/' + result.id + '/file';
    save.download = '';
    save.hidden = false;
    status.textContent = '视频已准备好。如果浏览器没有开始下载，请点击「保存 MP4」。';
    if (apiBase === location.origin) save.click();
    return;
   }
  }
  throw new Error('等待超时，请稍后重试。');
 } catch (error) {
  status.textContent = error instanceof TypeError || error.name === 'TimeoutError'
   ? (hosted ? '下载服务连接失败，请稍后重试。' : '无法连接本机下载服务。请先运行启动脚本，或打开本机网页版再试。')
   : error.message;
 } finally {
  start.disabled = false;
 }
});
