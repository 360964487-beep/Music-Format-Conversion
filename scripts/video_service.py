"""Video download service for the QingZhuan web app."""
import hashlib
import json
import os
import re
import secrets
import shutil
import subprocess
import tempfile
import threading
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import quote, urljoin, urlsplit
from urllib.request import HTTPRedirectHandler, build_opener, urlopen

PUBLIC_ORIGIN = (os.environ.get("QZ_PUBLIC_ORIGIN") or os.environ.get("RENDER_EXTERNAL_URL") or "").rstrip("/")
PORT = int(os.environ.get("PORT", "10000")) if PUBLIC_ORIGIN else 18746
ADDRESS = PUBLIC_ORIGIN or f"http://127.0.0.1:{PORT}"
HOST = urlsplit(ADDRESS).netloc
BIND = "0.0.0.0" if PUBLIC_ORIGIN else "127.0.0.1"
ORIGIN = "https://360964487-beep.github.io"
ROOT = Path(__file__).resolve().parents[1]
RELEASE = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.10.26/yt-dlp.exe"
SHA256 = "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d"
LOCK = threading.Lock()
JOBS = {}
MAX_VIDEO = 400 * 1024 * 1024
LAST_START = 0.0


def parse_link(text):
    for candidate in re.findall(r'https?://[^\s<>"。，！；）]+', text, re.I):
        candidate = candidate.rstrip(".,)")
        try:
            url = urlsplit(candidate)
            if url.username is not None or url.port not in (None, 80 if url.scheme == "http" else 443):
                continue
        except ValueError:
            continue
        if (url.hostname in ("b23.tv", "v.douyin.com") and re.fullmatch(r"/[A-Za-z0-9]+/?", url.path)
                or url.hostname in ("bilibili.com", "www.bilibili.com", "m.bilibili.com")
                and re.fullmatch(r"/video/(?:BV[a-z0-9]+|av\d+)/?", url.path, re.I)
                or url.hostname in ("douyin.com", "www.douyin.com")
                and re.fullmatch(r"/video/\d+/?", url.path)):
            return candidate, url.hostname in ("v.douyin.com", "douyin.com", "www.douyin.com")
    raise ValueError("请粘贴 B 站或抖音的单个视频链接或分享文案。")


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def resolve_public_link(link, douyin):
    short_hosts = ("v.douyin.com",) if douyin else ("b23.tv",)
    opener = build_opener(NoRedirect())
    for _ in range(4):
        if urlsplit(link).hostname not in short_hosts:
            return link
        try:
            with opener.open(link, timeout=12):
                break
        except HTTPError as error:
            location = error.headers.get("Location")
            if error.code not in (301, 302, 303, 307, 308) or not location:
                break
            target = urljoin(link, location)
            try:
                link, target_douyin = parse_link(target)
            except ValueError:
                break
            if target_douyin != douyin or link != target:
                break
    raise RuntimeError("无法展开分享短链，请使用浏览器地址栏中的视频链接。")


def command(directory):
    if PUBLIC_ORIGIN:
        return ["yt-dlp"]
    exe = ROOT / "bin" / "yt-dlp.exe"
    if exe.exists() and hashlib.sha256(exe.read_bytes()).hexdigest() == SHA256:
        return [str(exe)]
    staged = directory / "yt-dlp.exe"
    try:
        with urlopen(RELEASE, timeout=25) as response:
            binary = response.read(40 * 1024 * 1024)
    except OSError as error:
        raise RuntimeError("无法从 GitHub 获取下载组件，请检查网络。") from error
    if hashlib.sha256(binary).hexdigest() != SHA256:
        raise RuntimeError("下载组件校验失败。")
    staged.write_bytes(binary)
    try:
        exe.parent.mkdir(exist_ok=True)
        staged.replace(exe)
        return [str(exe)]
    except OSError:
        return [str(staged)]


def run(job, link, douyin, cookies):
    with tempfile.TemporaryDirectory(prefix="QingZhuan-web-") as temp:
        folder = Path(temp)
        try:
            if PUBLIC_ORIGIN:
                link = resolve_public_link(link, douyin)
            ffmpeg = Path("/usr/bin/ffmpeg") if PUBLIC_ORIGIN else ROOT / "bin" / "ffmpeg.exe"
            if not ffmpeg.is_file():
                raise RuntimeError("缺少 FFmpeg，请完整解压网页下载服务包。")
            args = command(folder) + [
                "--ignore-config", "--no-playlist", "--playlist-items", "1",
                "--socket-timeout", "20", "--retries", "2", "--max-filesize", "400M",
                "--windows-filenames", "--ffmpeg-location", str(ffmpeg.parent),
                "-f", "bv[ext=mp4]+ba[ext=m4a]/b[ext=mp4]", "--merge-output-format", "mp4",
                "-o", "%(title).80B [%(id)s].%(ext)s",
            ]
            if PUBLIC_ORIGIN:
                args += ["--use-extractors", "douyin" if douyin else "bilibili"]
            if douyin and cookies:
                lines = [line for line in cookies.splitlines() if len(line.split("\t")) == 7
                         and re.fullmatch(r"(?:#HttpOnly_)?\.?([a-z0-9-]+\.)*douyin\.com", line.split("\t")[0], re.I)]
                if not lines:
                    raise RuntimeError("所选文件没有可用的抖音 Cookie。")
                cookie_path = folder / "cookies.txt"
                cookie_path.write_text("# Netscape HTTP Cookie File\n" + "\n".join(lines) + "\n", encoding="utf-8")
                args += ["--cookies", str(cookie_path)]
            result = subprocess.run(args + ["--", link], cwd=folder, capture_output=True, timeout=600)
            if result.returncode:
                if douyin and b"cookies" in result.stderr.lower():
                    raise RuntimeError("抖音要求站点验证，此视频暂时无法在线下载。" if PUBLIC_ORIGIN
                                       else "抖音要求有效 Cookie。选择本地 Cookie 文件后重试。")
                raise RuntimeError("平台未返回可下载的视频，请检查链接、网络及视频是否可访问。")
            files = list(folder.glob("*.mp4"))
            if len(files) != 1 or not 0 < files[0].stat().st_size <= MAX_VIDEO:
                raise RuntimeError("没有得到可保存的 MP4，或视频超过 400 MB。")
            artifact = Path(job["folder"]) / files[0].name
            shutil.move(files[0], artifact)
            with LOCK:
                job.update(status="ready", file=artifact)
        except subprocess.TimeoutExpired:
            with LOCK:
                job.update(status="error", error="下载超时，请重试。")
        except (RuntimeError, OSError) as error:
            with LOCK:
                job.update(status="error", error=str(error))
        finally:
            with LOCK:
                job["ended"] = time.monotonic()


def reap():
    while True:
        time.sleep(30)
        with LOCK:
            expired = [key for key, job in JOBS.items() if job.get("ended", float("inf")) < time.monotonic() - 900
                       and not job.get("sending")]
            for key in expired:
                shutil.rmtree(JOBS.pop(key)["folder"], ignore_errors=True)


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args):
        super().__init__(*args, directory=str(ROOT / "web"))

    def setup(self):
        super().setup()
        self.connection.settimeout(15)

    def host_allowed(self):
        return self.headers.get("Host") == HOST

    def allowed(self):
        allowed_origins = (ADDRESS,) if PUBLIC_ORIGIN else (ORIGIN, ADDRESS)
        return self.host_allowed() and self.headers.get("Origin") in allowed_origins

    def headers_for(self, status, kind, length):
        self.send_response(status)
        if self.allowed():
            self.send_header("Access-Control-Allow-Origin", self.headers["Origin"])
            self.send_header("Vary", "Origin")
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(length))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")

    def respond(self, status, data):
        body = json.dumps(data, ensure_ascii=False).encode()
        self.headers_for(status, "application/json", len(body))
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        if not self.allowed():
            return self.respond(403, {})
        self.headers_for(204, "text/plain", 0)
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Access-Control-Allow-Private-Network", "true")
        self.end_headers()

    def do_POST(self):
        global LAST_START
        if not self.allowed() or self.path != "/jobs" or self.headers.get_content_type() != "application/json":
            return self.respond(403, {})
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if not 0 < size <= 100_000:
                raise ValueError("分享文案或 Cookie 文件过长。")
            data = json.loads(self.rfile.read(size))
            if not isinstance(data, dict):
                raise ValueError("下载请求格式无效。")
            text, cookies = data.get("text"), data.get("cookies", "")
            if not isinstance(text, str) or len(text) > 4096 or not isinstance(cookies, str):
                raise ValueError("分享文案或 Cookie 文件格式无效。")
            if PUBLIC_ORIGIN and cookies:
                raise ValueError("线上下载服务不接收浏览器 Cookie。")
            link, douyin = parse_link(text)
        except (ValueError, OSError) as error:
            return self.respond(400, {"error": str(error)})
        with LOCK:
            busy = len(JOBS) >= 4 or any(job["status"] == "working" for job in JOBS.values())
            busy = busy or bool(PUBLIC_ORIGIN and time.monotonic() - LAST_START < 5)
            if not busy:
                LAST_START = time.monotonic()
                key = secrets.token_urlsafe(24)
                job = {"status": "working", "folder": tempfile.mkdtemp(prefix="QingZhuan-video-")}
                JOBS[key] = job
        if busy:
            return self.respond(429, {"error": "已有下载任务或待保存文件，请稍后重试。"})
        threading.Thread(target=run, args=(job, link, douyin, cookies), daemon=True).start()
        self.respond(202, {"id": key})

    def do_GET(self):
        match = re.fullmatch(r"/jobs/([A-Za-z0-9_-]{32})(/file)?", self.path)
        if self.path == "/healthz":
            return self.respond(200, {"status": "ok"})
        if not self.host_allowed():
            return self.respond(403, {})
        if not match:
            if self.path.startswith("/jobs"):
                return self.respond(404, {})
            return super().do_GET()
        key, download = match.groups()
        with LOCK:
            job = JOBS.get(key)
            file = job.get("file") if job else None
            if download and file:
                if job.get("sending"):
                    file = None
                else:
                    job["sending"] = True
            info = {field: job[field] for field in ("status", "error") if field in job} if job else {}
        if not job or (download and not file):
            return self.respond(404, {})
        if not download:
            if info.get("status") == "error":
                with LOCK:
                    JOBS.pop(key, None)
                shutil.rmtree(job["folder"], ignore_errors=True)
            return self.respond(200, info)
        try:
            with file.open("rb") as source:
                self.headers_for(200, "video/mp4", file.stat().st_size)
                self.send_header("Content-Disposition", "attachment; filename*=UTF-8''" + quote(file.name))
                self.end_headers()
                shutil.copyfileobj(source, self.wfile, 128 * 1024)
        except (OSError, TimeoutError):
            with LOCK:
                job["sending"] = False
        else:
            with LOCK:
                JOBS.pop(key, None)
            shutil.rmtree(job["folder"], ignore_errors=True)

    def list_directory(self, path):
        self.send_error(404)
        return None

    def log_message(self, *_):
        pass


if __name__ == "__main__":
    if PUBLIC_ORIGIN and urlsplit(PUBLIC_ORIGIN).scheme != "https":
        raise SystemExit("QZ_PUBLIC_ORIGIN must be the public HTTPS origin.")
    with ThreadingHTTPServer((BIND, PORT), Handler) as server:
        threading.Thread(target=reap, daemon=True).start()
        print(f"轻转网页下载服务已启动。请保持窗口打开，并访问 {ADDRESS}/", flush=True)
        server.serve_forever()
