using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public static class VideoDownload
{
    const string EngineUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.10.26/yt-dlp.exe";
    const string EngineHash = "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d";

    public static Uri ReadLink(string text)
    {
        foreach (Match candidate in Regex.Matches(text ?? "", @"https?://[^\s<>""\u3000\u3002\uff0c\uff01\uff1b\uff09]+", RegexOptions.IgnoreCase))
        {
            Uri link;
            if (!Uri.TryCreate(candidate.Value.TrimEnd('.', ',', ')'), UriKind.Absolute, out link)
                || !link.IsDefaultPort || link.UserInfo.Length != 0) continue;
            string host = link.IdnHost.ToLowerInvariant();
            string path = link.AbsolutePath;
            if (host == "b23.tv" || host == "v.douyin.com"
                || ((host == "www.bilibili.com" || host == "bilibili.com" || host == "m.bilibili.com")
                    && Regex.IsMatch(path, @"^/video/(BV[0-9A-Za-z]+|av\d+)(/|$)", RegexOptions.IgnoreCase))
                || ((host == "www.douyin.com" || host == "douyin.com")
                    && Regex.IsMatch(path, @"^/video/\d+(/|$)")))
                return link;
        }
        throw new ArgumentException("请粘贴 B 站或抖音的单个视频链接，也可以粘贴完整分享文案。");
    }

    static string Downloader(Action<string> report)
    {
        string bin = Path.Combine(Engine.Base, "bin");
        string executable = Path.Combine(bin, "yt-dlp.exe");
        if (File.Exists(executable)) return executable;
        Directory.CreateDirectory(bin);
        string staging = executable + "." + Guid.NewGuid().ToString("N") + ".tmp";
        report("首次使用，正在获取下载组件…");
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(EngineUrl);
            request.UserAgent = "QingZhuan";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            using (var response = request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var output = File.Create(staging)) input.CopyTo(output);
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(staging))
                if (!String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), EngineHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("下载组件校验失败，请重试。");
            try { File.Move(staging, executable); }
            catch (IOException) { if (!File.Exists(executable)) throw; }
            return executable;
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    public static string Download(Uri link, string destination, string cookieFile, Action<string> report)
    {
        if (!File.Exists(Engine.Ffmpeg)) throw new FileNotFoundException("缺少 FFmpeg，请完整解压轻转。");
        string executable = Downloader(report);
        Directory.CreateDirectory(destination);
        string stage = Path.Combine(destination, ".QingZhuan-video-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        string localCookies = null;
        try
        {
            string args = "--ignore-config --no-playlist --no-color --newline --no-mtime --socket-timeout 20 --retries 2"
                + " --windows-filenames --ffmpeg-location " + Quote(Path.GetDirectoryName(Engine.Ffmpeg))
                + " -f " + Quote("bv[ext=mp4]+ba[ext=m4a]/b[ext=mp4]")
                + " --merge-output-format mp4 -o " + Quote("%(title).80B [%(id)s].%(ext)s");
            if (!String.IsNullOrEmpty(cookieFile))
            {
                localCookies = Path.Combine(Path.GetTempPath(), "QingZhuan-cookies-" + Guid.NewGuid().ToString("N") + ".txt");
                using (var writer = new StreamWriter(localCookies, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# Netscape HTTP Cookie File");
                    int count = 0;
                    foreach (string line in File.ReadLines(cookieFile))
                    {
                        string domain = line.Split('\t')[0].Replace("#HttpOnly_", "").TrimStart('.');
                        if (domain.Equals("douyin.com", StringComparison.OrdinalIgnoreCase)
                            || domain.EndsWith(".douyin.com", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(line);
                            count++;
                        }
                    }
                    if (count == 0) throw new ArgumentException("该文件没有可用的抖音 Cookie。");
                }
                args += " --cookies " + Quote(localCookies);
            }
            args += " -- " + Quote(link.AbsoluteUri);

            report("正在解析链接…");
            var settings = new ProcessStartInfo(executable, args) {
                WorkingDirectory = stage, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            settings.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using (var process = Process.Start(settings))
            {
                var stderr = process.StandardError.ReadToEndAsync();
                string line;
                var progress = new Regex(@"^\[download\]\s+(\d+(?:\.\d+)?%)");
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    var percent = progress.Match(line);
                    if (percent.Success) report("正在下载 " + percent.Groups[1].Value);
                    else if (line.StartsWith("[Merger]")) report("正在合并视频与音频…");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    string detail = stderr.Result.Trim();
                    if (detail.IndexOf("cookies", StringComparison.OrdinalIgnoreCase) >= 0
                        && (link.Host == "v.douyin.com" || link.Host.EndsWith("douyin.com", StringComparison.Ordinal)))
                        throw new IOException("抖音要求有效的 Cookie。请选择最新的抖音 Cookie 文件后重试。");
                    throw new IOException("下载失败。请检查链接、网络及视频可用性。\n" + detail.Substring(Math.Max(0, detail.Length - 380)));
                }
            }
            var files = Directory.GetFiles(stage, "*.mp4");
            if (files.Length == 0) throw new IOException("没有生成 MP4 文件。请检查该链接是否为视频。");
            string saved = null;
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                for (int n = 0; ; n++)
                {
                    string target = Path.Combine(destination, name + (n == 0 ? "" : " (" + n + ")") + ".mp4");
                    try { File.Move(file, target); saved = target; break; }
                    catch (IOException) { if (!File.Exists(target)) throw; }
                }
            }
            return saved;
        }
        finally
        {
            if (localCookies != null && File.Exists(localCookies)) File.Delete(localCookies);
            Directory.Delete(stage, true);
        }
    }

    static string Quote(string value)
    {
        if (value.IndexOf('"') >= 0) throw new ArgumentException("路径或链接中含有无效引号。");
        return "\"" + value + "\"";
    }
}
