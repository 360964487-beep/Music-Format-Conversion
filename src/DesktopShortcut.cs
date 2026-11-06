using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;

public static class DesktopShortcut
{
    public static string Create(string desktop, string executable)
    {
        if (!Directory.Exists(desktop))
            throw new DirectoryNotFoundException("找不到 Windows 桌面目录。");

        string path = Path.Combine(desktop, "轻转（Windows）.lnk");
        object shellLink = new ShellLink();
        try
        {
            var shortcut = (IShellLinkW)shellLink;
            shortcut.SetPath(executable);
            shortcut.SetWorkingDirectory(Path.GetDirectoryName(executable));
            shortcut.SetIconLocation(executable, 0);
            shortcut.SetDescription("轻转 Windows 版");
            ((IPersistFile)shellLink).Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
        return path;
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        // The declaration order matches the Windows Shell Link COM interface.
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr data, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
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
