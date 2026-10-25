using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Web.Script.Serialization;

public class Verify {
 static string scratch,root;
 static List<string> passed=new List<string>();
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Pass(string message){passed.Add(message);Console.WriteLine("PASS "+message);}
 static string P(string name){return Path.Combine(scratch,name);}
 static string Quote(string value){return "\""+value+"\"";}
 static void FF(string args){Engine.Run("-hide_banner -loglevel error -nostdin -y "+args,CancellationToken.None,p=>{},0);}
 static byte[] Encrypt(byte[] value,string key){using(var a=Aes.Create()){a.Key=Encoding.ASCII.GetBytes(key);a.Mode=CipherMode.ECB;a.Padding=PaddingMode.PKCS7;using(var c=a.CreateEncryptor())return c.TransformFinalBlock(value,0,value.Length);}}
 static void NcmFixture(string audio,string file,bool metadata){
  byte[] key=Encoding.ASCII.GetBytes("fixture-key-0123456789");var raw=Encrypt(Encoding.ASCII.GetBytes("neteasecloudmusic").Concat(key).ToArray(),"hzHRAmso5kInbaxW").Select(v=>(byte)(v^0x64)).ToArray();
  byte[] meta=new byte[0],cover=new byte[0];
  if(metadata){string json=new JavaScriptSerializer().Serialize(new{musicName="测试歌名",album="测试专辑",artist=new object[]{new object[]{"测试歌手",1}}});meta=Encoding.ASCII.GetBytes("163 key(Don't modify):"+Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes("music:"+json),"#14ljk_!\\]&0U<'("))).Select(v=>(byte)(v^0x63)).ToArray();using(var image=new System.Drawing.Bitmap(16,16))using(var stream=new MemoryStream()){image.Save(stream,System.Drawing.Imaging.ImageFormat.Jpeg);cover=stream.ToArray();}}
  int[] box=Enumerable.Range(0,256).ToArray();int j=0;for(int i=0;i<256;i++){j=(j+box[i]+key[i%key.Length])%256;int v=box[i];box[i]=box[j];box[j]=v;}var data=File.ReadAllBytes(audio);for(int i=0;i<data.Length;i++){int a=(i+1)%256;data[i]^=(byte)box[(box[a]+box[(box[a]+a)%256])%256];}
  using(var w=new BinaryWriter(File.Create(file))){w.Write(Encoding.ASCII.GetBytes("CTENFDAM"));w.Write((short)0);w.Write(raw.Length);w.Write(raw);w.Write(meta.Length);w.Write(meta);w.Write(new byte[5]);w.Write(cover.Length+19);w.Write(cover.Length);w.Write(cover);w.Write(new byte[19]);w.Write(data);}
 }
 static string ConvertFile(string name){var song=new Song{Path=P(name)};if(File.Exists(Path.ChangeExtension(song.Path,".lrc")))song.Lyric=Path.ChangeExtension(song.Path,".lrc");return Engine.ConvertSong(song,P("输出"),320,CancellationToken.None,p=>{});}
 static string Capture(string exe,string args){using(var process=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8})){var err=process.StandardError.ReadToEndAsync();var output=process.StandardOutput.ReadToEndAsync();if(!process.WaitForExit(30000)){process.Kill();throw new Exception("测试进程超时");}Assert(process.ExitCode==0,err.Result);return output.Result;}}
 static void ConvertTests(){
  FF("-f lavfi -i sine=frequency=440:duration=2 -metadata title=FLAC测试标题 "+Quote(P("输入 音频.flac")));
  FF("-i "+Quote(P("输入 音频.flac"))+" -c:a libmp3lame -b:a 128k "+Quote(P("原始.mp3")));
  File.WriteAllText(P("输入 音频.lrc"),"[00:00.00]测试歌词",Encoding.UTF8);
  var output=ConvertFile("输入 音频.flac");Assert(File.ReadAllBytes(Path.ChangeExtension(output,".lrc")).SequenceEqual(File.ReadAllBytes(P("输入 音频.lrc"))),"歌词不一致");Assert(new FileInfo(output).Length>1000,"MP3结果为空");Pass("FLAC转MP3、中文空格路径、歌词配套保存");
  string second=ConvertFile("输入 音频.flac");Assert(second!=output&&File.Exists(output),"重名覆盖");Pass("重名自动编号、保留源文件");
  NcmFixture(P("原始.mp3"),P("MP3封装.ncm"),true);NcmFixture(P("输入 音频.flac"),P("FLAC封装.ncm"),true);NcmFixture(P("原始.mp3"),P("无元数据.ncm"),false);
  string ncm=ConvertFile("MP3封装.ncm");ConvertFile("FLAC封装.ncm");ConvertFile("无元数据.ncm");Pass("NCM内含MP3/FLAC、无元数据、封面帧填充");
  string info=Engine.Run("-hide_banner -loglevel info -i "+Quote(ncm)+" -f null -",CancellationToken.None,p=>{},0);Assert(info.Contains("测试歌名")&&info.Contains("测试歌手")&&info.Contains("测试专辑")&&info.Contains("attached pic"),"元数据或封面丢失");Assert(info.Contains("128 kb/s"),"原MP3码率改变");Pass("歌曲信息与封面保留，MP3音频保持原码率");
  FF("-i "+Quote(P("原始.mp3"))+" -map 0:a:0 -f s16le "+Quote(P("before.pcm")));FF("-i "+Quote(ncm)+" -map 0:a:0 -f s16le "+Quote(P("after.pcm")));Assert(File.ReadAllBytes(P("before.pcm")).SequenceEqual(File.ReadAllBytes(P("after.pcm"))),"提取后音频发生变化");Pass("NCM内含MP3提取后的解码音频逐字节一致");
  File.WriteAllBytes(P("损坏.ncm"),new byte[]{1,2,3});bool rejected=false;try{ConvertFile("损坏.ncm");}catch(InvalidDataException){rejected=true;}Assert(rejected,"未拒绝损坏文件");Pass("损坏NCM拒绝处理");
  FF("-f lavfi -i sine=frequency=440:duration=600 "+Quote(P("long.flac")));var cts=new CancellationTokenSource();cts.CancelAfter(80);bool cancelled=false;string dir=P("cancel-out");try{Engine.ConvertSong(new Song{Path=P("long.flac")},dir,320,cts.Token,p=>{});}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"取消未生效");Assert(!Directory.GetFiles(dir).Any()&&!Directory.GetDirectories(dir).Any(),"取消后留下半成品");Pass("取消真实转码并清理临时文件");
 }
 static void PreferenceTests(){
  string path=P("preferences.json");var p=new Preferences{InputDirectory=scratch,OutputDirectory=P("输出 音乐"),Theme=2,Quality=3};PreferenceStore.Save(p,path);p.Theme=3;PreferenceStore.Save(p,path);var loaded=PreferenceStore.Load(path);Assert(loaded.InputDirectory==scratch&&loaded.OutputDirectory==p.OutputDirectory&&loaded.Theme==3&&loaded.Quality==3,"偏好恢复错误");Assert(!Directory.GetFiles(scratch,"*.tmp").Any(),"设置临时文件残留");
  string probe=Capture(System.Reflection.Assembly.GetExecutingAssembly().Location,"--prefs-probe "+Quote(path));Assert(probe.Contains("3|3|"+scratch+"|"+p.OutputDirectory),"跨进程偏好恢复错误");Pass("默认添加/保存目录、主题、音质跨进程恢复与原子更新");
  File.WriteAllText(path,"broken-json");Assert(PreferenceStore.Load(path).Theme==0&&PreferenceStore.Warning!=null,"损坏设置未回退");Assert(PreferenceStore.DirectoryValue("",false)=="","默认目录未支持空白");bool invalid=false;try{PreferenceStore.DirectoryValue("relative/path",false);}catch(ArgumentException){invalid=true;}Assert(invalid,"接受相对目录");Pass("空目录默认值、非法路径拒绝、损坏设置回退");
  string isolated=P("instance");Directory.CreateDirectory(Path.Combine(isolated,"src"));File.Copy(Path.Combine(root,"轻转.exe"),Path.Combine(isolated,"轻转.exe"));foreach(string f in Directory.GetFiles(Path.Combine(root,"src"),"*.xaml"))File.Copy(f,Path.Combine(isolated,"src",Path.GetFileName(f)));PreferenceStore.Save(p,Path.Combine(isolated,"settings.json"));string smoke=P("ui-smoke.txt");Capture(Path.Combine(isolated,"轻转.exe"),"--ui-smoke "+Quote(smoke));string text=File.ReadAllText(smoke);Assert(text.Contains(p.OutputDirectory)&&text.Contains("\n3\n")&&text.Contains(scratch),"真实窗口未恢复偏好");Pass("真实窗口启动、恢复默认目录与主题、正常退出");
 }
 static FrameworkElement Detach(Window w){var f=(FrameworkElement)w.Content;w.Content=null;f.Resources=w.Resources;System.Windows.Documents.TextElement.SetFontFamily(f,w.FontFamily);System.Windows.Documents.TextElement.SetFontSize(f,w.FontSize);System.Windows.Documents.TextElement.SetForeground(f,w.Foreground);return f;}
 static void Render(Window w,string file,int width,int height){var c=Detach(w);c.Margin=new Thickness(28,22,28,22);c.Width=width-56;c.Height=height-44;((Grid)c).Background=w.Background;c.Measure(new Size(width,height));c.Arrange(new Rect(0,0,width,height));c.UpdateLayout();var q=w.FindName("Queue")as DataGrid;if(q!=null){q.Columns[0].Width=new DataGridLength(Math.Max(140,q.ActualWidth-482));c.UpdateLayout();}
  foreach(string name in new[]{"Start","Quality","Destination","Queue","ThemeChoice","Settings"}){var e=w.FindName(name)as FrameworkElement;if(e==null)continue;var pos=e.TranslatePoint(new Point(0,0),c);Assert(pos.X>=-1&&pos.Y>=-1&&pos.X+e.ActualWidth<=c.ActualWidth+1&&pos.Y+e.ActualHeight<=c.ActualHeight+1,"控件越界："+name);}
  var v=new DrawingVisual();using(var dc=v.RenderOpen()){dc.DrawRectangle(w.Background,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(c),null,new Rect(28,22,width-56,height-44));}var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);b.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(file))png.Save(f);
 }
 static void VisualTests(){string docs=Path.Combine(root,"docs");Directory.CreateDirectory(docs);for(int i=0;i<4;i++){Window w;using(var file=File.OpenRead(Path.Combine(root,"src","Main.xaml")))w=(Window)XamlReader.Load(file);Looks.Attach(w,i);((ComboBox)w.FindName("ThemeChoice")).SelectedIndex=i;((TextBlock)w.FindName("ImportHint")).Text=@"默认添加目录：D:\音乐\网易云下载";((TextBox)w.FindName("Destination")).Text=@"D:\音乐\MP3";var q=(DataGrid)w.FindName("Queue");q.ItemsSource=new[]{new Song{Path=P("输入 音频.flac"),Lyric="test.lrc"},new Song{Path=P("MP3封装.ncm")},new Song{Path=P("FLAC封装.ncm")}};((TextBlock)w.FindName("Count")).Text="转换队列 · 3 首歌曲";((Button)w.FindName("Start")).IsEnabled=true;Render(w,Path.Combine(docs,"theme-"+i+".png"),1100,760);}
  Window dialog;using(var f=File.OpenRead(Path.Combine(root,"src","Settings.xaml")))dialog=(Window)XamlReader.Load(f);Looks.Attach(dialog,1);((TextBox)dialog.FindName("DefaultInput")).Text=@"D:\音乐\网易云下载";((TextBox)dialog.FindName("DefaultOutput")).Text=@"D:\音乐\MP3";Render(dialog,Path.Combine(docs,"settings.png"),660,440);Pass("四套主题与目录设置界面渲染、控件边界检查");
 }
 [STAThread]public static int Main(string[] args){Console.OutputEncoding=new UTF8Encoding(false);try{if(args.Length>1&&args[0]=="--prefs-probe"){var p=PreferenceStore.Load(args[1]);Console.WriteLine(p.Theme+"|"+p.Quality+"|"+p.InputDirectory+"|"+p.OutputDirectory);return 0;}root=Path.GetFullPath(args[0]);Engine.Base=root;scratch=Path.Combine(root,"tests-output","run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);ConvertTests();PreferenceTests();VisualTests();File.WriteAllText(Path.Combine(root,"docs","验证结果.txt"),"测试使用合成音频和合成NCM封装；尚未验证所有真实网易云客户端版本。\n\n"+string.Join("\n",passed.Select(p=>"通过："+p)),Encoding.UTF8);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}


