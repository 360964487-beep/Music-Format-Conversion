const local = 'http://127.0.0.1:18746';
const dialog = document.querySelector('#videoDialog');
const form = document.querySelector('#videoForm');
const start = document.querySelector('#startVideo');
const status = document.querySelector('#videoStatus');
const save = document.querySelector('#saveVideo');
const link = document.querySelector('#videoLink');
const cookies = document.querySelector('#videoCookies');

if (location.origin === local) document.querySelector('#videoSetup').hidden = true;
document.querySelector('#openVideo').addEventListener('click', () => dialog.showModal());

async function api(path, options) {
 const response = await fetch(local + path, {...options, signal: AbortSignal.timeout(15000)});
 const result = await response.json();
 if (!response.ok) throw new Error(result.error || '下载服务返回错误，请重试。');
 return result;
}

form.addEventListener('submit', async event => {
 event.preventDefault();
 if (start.disabled) return;
 const file = cookies.files[0];
 if (file?.size > 80000) {
  status.textContent = 'Cookie 文件超过 80 KB，请选择仅含抖音站点的文件。';
  return;
 }
 start.disabled = true;
 save.hidden = true;
 status.textContent = '正在连接本机下载服务…';
 try {
  const result = await api('/jobs', {
   method: 'POST',
   headers: {'Content-Type': 'application/json'},
   body: JSON.stringify({text: link.value.trim(), cookies: file ? await file.text() : ''})
  });
  status.textContent = '正在解析并下载视频，请保持下载服务运行…';
  const deadline = Date.now() + 660000;
  while (Date.now() < deadline) {
   await new Promise(resolve => setTimeout(resolve, 1500));
   const job = await api('/jobs/' + result.id);
   if (job.status === 'error') throw new Error(job.error);
   if (job.status === 'ready') {
    save.href = local + '/jobs/' + result.id + '/file';
    save.target = '_blank';
    save.rel = 'noopener noreferrer';
    save.hidden = false;
    status.textContent = '视频已准备好，请点击「保存 MP4」。';
    return;
   }
  }
  throw new Error('等待超时，请稍后重试。');
 } catch (error) {
  status.textContent = error instanceof TypeError || error.name === 'TimeoutError'
   ? '无法连接本机下载服务。请先运行启动脚本，或打开本机网页版再试。'
   : error.message;
 } finally {
  start.disabled = false;
 }
});
