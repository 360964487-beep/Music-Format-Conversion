using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

static class VideoWindow
{
    public static string Show(Window owner, string folder)
    {
        Window dialog;
        using (var input = File.OpenRead(Path.Combine(Engine.Base, "src", "Video.xaml")))
            dialog = (Window)XamlReader.Load(input);
        dialog.Owner = owner;
        dialog.Resources = owner.Resources;
        var link = (TextBox)dialog.FindName("VideoLink");
        var status = (TextBlock)dialog.FindName("VideoStatus");
        var cookieStatus = (TextBlock)dialog.FindName("VideoCookieStatus");
        var progress = (ProgressBar)dialog.FindName("VideoProgress");
        var pickCookies = (Button)dialog.FindName("PickVideoCookies");
        var start = (Button)dialog.FindName("StartVideo");
        bool busy = false;
        string cookies = null;
        string savedFolder = null;
        pickCookies.Click += (s, e) => {
            if (cookies != null)
            {
                cookies = null;
                pickCookies.Content = "选择抖音 Cookie";
                cookieStatus.Text = "抖音 Cookie（按需）";
                return;
            }
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Netscape Cookie 文件|*.txt", Title = "选择仅含抖音站点的 Cookie 文件" };
            if (picker.ShowDialog(dialog) == true)
            {
                cookies = picker.FileName;
                pickCookies.Content = "移除 Cookie";
                cookieStatus.Text = picker.SafeFileName;
            }
        };
        dialog.Closing += (s, e) => { if (busy) e.Cancel = true; };
        start.Click += async (s, e) => {
            if (busy) return;
            Uri uri;
            try
            {
                uri = VideoDownload.ReadLink(link.Text);
            }
            catch (Exception error) { status.Text = error.Message; return; }

            busy = true;
            start.IsEnabled = link.IsEnabled = pickCookies.IsEnabled = false;
            progress.Visibility = Visibility.Visible;
            progress.IsIndeterminate = true;
            status.Text = "正在准备下载…";
            string cookieFile = uri.Host.EndsWith("douyin.com", StringComparison.OrdinalIgnoreCase) ? cookies : null;
            var reporter = new Progress<string>(message => { if (busy) status.Text = message; });
            try
            {
                string saved = await Task.Run(() => VideoDownload.Download(uri, folder, cookieFile, message => ((IProgress<string>)reporter).Report(message)));
                savedFolder = folder;
                status.Text = "已保存 " + Path.GetFileName(saved);
            }
            catch (Exception error) { status.Text = error.Message; }
            finally
            {
                busy = false;
                progress.IsIndeterminate = false;
                progress.Visibility = Visibility.Hidden;
                start.IsEnabled = link.IsEnabled = pickCookies.IsEnabled = true;
            }
        };
        dialog.Loaded += (s, e) => link.Focus();
        dialog.ShowDialog();
        return savedFolder;
    }
}
