using System;
using System.IO;
using System.Windows;

public static class DesktopShortcut
{
    public static string Create(string desktop, string executable)
    {
        if (!Directory.Exists(desktop))
            throw new DirectoryNotFoundException("找不到 Windows 桌面目录。");

        string path = Path.Combine(desktop, "轻转（Windows）.lnk");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = executable;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executable);
        shortcut.IconLocation = executable + ",0";
        shortcut.Description = "轻转 Windows 版";
        shortcut.Save();
        return path;
    }

    public static int Install()
    {
        try
        {
            Create(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                   System.Reflection.Assembly.GetExecutingAssembly().Location);
            MessageBox.Show("桌面快捷方式「轻转（Windows）」已创建。", "轻转");
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("无法创建桌面快捷方式：" + error.Message, "轻转");
            return 1;
        }
    }
}
