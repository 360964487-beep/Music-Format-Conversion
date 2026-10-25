using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Collections.ObjectModel;using System.ComponentModel;using System.Diagnostics;using System.Security.Cryptography;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Markup;using System.Web.Script.Serialization;using Forms=System.Windows.Forms;
public class Song:INotifyPropertyChanged{
 public string Path{get;set;}public string Lyric{get;set;}public string Output{get;set;}
 public string Name{get{return System.IO.Path.GetFileNameWithoutExtension(Path);}}public string Format{get{return System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();}}public string Size{get{return (new FileInfo(Path).Length/1048576.0).ToString("0.0")+" MB";}}public string LyricState{get{return Lyric==null?(Format=="LRC"?"未匹配音频":"无歌词"):"已匹配歌词";}}
 string status="等待转换";public string Status{get{return status;}set{status=value;Changed("Status");}}public void Changed(string s){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(s));}public event PropertyChangedEventHandler PropertyChanged;public override string ToString(){return Path+"\n"+Status;}
}
public class Engine{
 public static string Base=AppDomain.CurrentDomain.BaseDirectory;public static string Ffmpeg{get{return System.IO.Path.Combine(Base,"bin","ffmpeg.exe");}}static string Q(string s){return "\""+s.Replace("\"","")+"\"";}
 static byte[] Read(BinaryReader r,int n){if(n<0||n>67108864||n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("NCM 数据段损坏或超出限制");var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
 static byte[] Decrypt(byte[] b,string key){using(var a=Aes.Create()){a.Key=Encoding.ASCII.GetBytes(key);a.Mode=CipherMode.ECB;a.Padding=PaddingMode.PKCS7;using(var d=a.CreateDecryptor())return d.TransformFinalBlock(b,0,b.Length);}}
 public class Ncm{public string Audio,Cover;public Dictionary<string,object> Meta;}
 public static Ncm Decode(string input,string temp,CancellationToken token,Action<double> progress){var result=new Ncm();using(var r=new BinaryReader(File.OpenRead(input))){
 if(Encoding.ASCII.GetString(Read(r,8))!="CTENFDAM")throw new InvalidDataException("不是有效的 NCM 文件");Read(r,2);var key=Read(r,r.ReadInt32());for(int i=0;i<key.Length;i++)key[i]^=0x64;key=Decrypt(key,"hzHRAmso5kInbaxW");if(key.Length<=17||Encoding.ASCII.GetString(key,0,17)!="neteasecloudmusic")throw new InvalidDataException("不支持的 NCM 密钥");
 var box=Enumerable.Range(0,256).Select(x=>(byte)x).ToArray();int j=0;for(int i=0;i<256;i++){j=(j+box[i]+key[17+i%(key.Length-17)])&255;byte t=box[i];box[i]=box[j];box[j]=t;}var mask=new byte[256];for(int i=0;i<256;i++){int a=(i+1)&255;mask[i]=box[(box[a]+box[(box[a]+a)&255])&255];}
 var meta=Read(r,r.ReadInt32());if(meta.Length>22){try{for(int i=0;i<meta.Length;i++)meta[i]^=0x63;string json=Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(Encoding.ASCII.GetString(meta,22,meta.Length-22)),"#14ljk_!\\]&0U<'("));result.Meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json.Substring(6));}catch{result.Meta=null;}}
 Read(r,5);int frame=r.ReadInt32(),image=r.ReadInt32();if(frame<image||frame>67108864)throw new InvalidDataException("NCM 封面数据损坏");var cover=Read(r,image);Read(r,frame-image);if(cover.Length>0){result.Cover=System.IO.Path.Combine(temp,"cover");File.WriteAllBytes(result.Cover,cover);}
 long offset=0;string raw=System.IO.Path.Combine(temp,"audio.data");using(var w=File.Create(raw)){var b=new byte[65536];int n;while((n=r.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();for(int i=0;i<n;i++)b[i]^=mask[(offset+i)&255];w.Write(b,0,n);offset+=n;progress(.25*r.BaseStream.Position/r.BaseStream.Length);}}
 var h=new byte[4];using(var f=File.OpenRead(raw))f.Read(h,0,4);string ext=Encoding.ASCII.GetString(h)=="fLaC"?".flac":(h[0]==73&&h[1]==68&&h[2]==51||h[0]==255&&(h[1]&224)==224)?".mp3":null;if(ext==null)throw new InvalidDataException("无法识别 NCM 内部音频，可能是新版本封装");result.Audio=System.IO.Path.ChangeExtension(raw,ext);File.Move(raw,result.Audio);
 }return result;}
 public static string Run(string args,CancellationToken token,Action<double> progress,double duration){var psi=new ProcessStartInfo(Ffmpeg,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardErrorEncoding=Encoding.UTF8};using(var p=Process.Start(psi))using(token.Register(()=>{try{if(!p.HasExited)p.Kill();}catch{}})){var errors=p.StandardError.ReadToEndAsync();string line;while((line=p.StandardOutput.ReadLine())!=null){if(line.StartsWith("out_time_us=")){double t;if(double.TryParse(line.Substring(12),out t)&&duration>0)progress(.30+.69*Math.Min(1,t/1000000/duration));}}p.WaitForExit();token.ThrowIfCancellationRequested();string err=errors.Result;if(p.ExitCode!=0)throw new InvalidOperationException("FFmpeg："+err.Substring(Math.Max(0,err.Length-700)));return err;}}
 public static double Duration(string audio){var psi=new ProcessStartInfo(Ffmpeg,"-hide_banner -i "+Q(audio)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};using(var p=Process.Start(psi)){var stdout=p.StandardOutput.ReadToEndAsync();string err=p.StandardError.ReadToEnd();p.WaitForExit();var m=System.Text.RegularExpressions.Regex.Match(err,@"Duration: (\d+):(\d+):(\d+\.\d+)");if(!m.Success)return 0;return double.Parse(m.Groups[1].Value)*3600+double.Parse(m.Groups[2].Value)*60+double.Parse(m.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture);}}
 static string Tag(string key,object value){return value==null?"":" -metadata "+key+"="+Q(Convert.ToString(value));}
 public static string ConvertSong(Song song,string destination,int quality,CancellationToken token,Action<double> progress){if(!File.Exists(Ffmpeg))throw new FileNotFoundException("缺少 bin/ffmpeg.exe");string folder=string.IsNullOrWhiteSpace(destination)?System.IO.Path.Combine(System.IO.Path.GetDirectoryName(song.Path),"MP3"):destination;Directory.CreateDirectory(folder);string temp=System.IO.Path.Combine(folder,".QingZhuan-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string partial=null;try{
 string audio=song.Path;Ncm ncm=null;if(song.Format=="NCM"){ncm=Decode(audio,temp,token,progress);audio=ncm.Audio;}bool copy=System.IO.Path.GetExtension(audio).Equals(".mp3",StringComparison.OrdinalIgnoreCase);string target=System.IO.Path.Combine(folder,song.Name+".mp3");int suffix=1;while(File.Exists(target)||File.Exists(System.IO.Path.ChangeExtension(target,".lrc")))target=System.IO.Path.Combine(folder,song.Name+" ("+(suffix++)+").mp3");partial=System.IO.Path.Combine(folder,"."+Guid.NewGuid().ToString("N")+".partial.mp3");
 string args="-hide_banner -loglevel error -nostdin -y -i "+Q(audio);if(ncm!=null&&ncm.Cover!=null)args+=" -i "+Q(ncm.Cover);args+=" -map 0:a:0";if(ncm!=null&&ncm.Cover!=null)args+=" -map 1:v:0 -c:v copy -disposition:v attached_pic";else args+=" -map 0:v? -c:v copy";args+=" -map_metadata 0 -id3v2_version 3 "+(copy?"-c:a copy":"-c:a libmp3lame -b:a "+quality+"k");
 if(ncm!=null&&ncm.Meta!=null){object v;if(ncm.Meta.TryGetValue("musicName",out v))args+=Tag("title",v);if(ncm.Meta.TryGetValue("album",out v))args+=Tag("album",v);if(ncm.Meta.TryGetValue("artist",out v)){var artists=v as System.Collections.IEnumerable;if(artists!=null)args+=Tag("artist",string.Join(" / ",artists.Cast<object>().Select(x=>{var a=x as System.Collections.IList;return a!=null&&a.Count>0?Convert.ToString(a[0]):"";})));}}
 args+=" -progress pipe:1 "+Q(partial);progress(.3);Run(args,token,progress,Duration(audio));token.ThrowIfCancellationRequested();if(new FileInfo(partial).Length<100)throw new InvalidDataException("转换结果为空");File.Move(partial,target);partial=null;song.Output=target;if(song.Lyric!=null){try{File.Copy(song.Lyric,System.IO.Path.ChangeExtension(target,".lrc"),false);}catch(Exception e){throw new IOException("音频已保存，歌词复制失败："+e.Message);}}progress(1);return target;
 }finally{if(partial!=null&&File.Exists(partial))File.Delete(partial);try{Directory.Delete(temp,true);}catch{}}}
}
public class App {
 static string smoke;
 Window w;
 ObservableCollection<Song> songs=new ObservableCollection<Song>();
 List<string> lyrics=new List<string>();
 bool busy,loading;
 CancellationTokenSource cts;
 string lastFolder;
 Preferences preferences;
 T Get<T>(string name)where T:class {return w.FindName(name)as T;}
 void Ui(Action a){w.Dispatcher.Invoke(a);}
 void Message(string s){MessageBox.Show(w,s,"轻转",MessageBoxButton.OK,MessageBoxImage.Information);}
 void Update(){int n=songs.Count(s=>s.Format!="LRC");Get<TextBlock>("Count").Text="转换队列 · "+n+" 首歌曲";Get<Button>("Start").IsEnabled=!busy&&n>0;}
 string InputFolder(){if(Directory.Exists(preferences.InputDirectory))return preferences.InputDirectory;string music=Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);return Directory.Exists(music)?music:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);}
 void RefreshHint(){var h=Get<TextBlock>("ImportHint");h.Text="默认添加目录："+(string.IsNullOrEmpty(preferences.InputDirectory)?"系统音乐文件夹":preferences.InputDirectory);h.ToolTip=string.IsNullOrEmpty(preferences.InputDirectory)?InputFolder():preferences.InputDirectory;}
 bool SavePreferences(bool notify){try{PreferenceStore.Save(preferences,PreferenceStore.DefaultPath);return true;}catch(Exception e){string text="设置未保存："+e.Message;Get<TextBlock>("Summary").Text=text;if(notify)Message(text);return false;}}
 bool SaveOutput(bool notify){try{preferences.OutputDirectory=PreferenceStore.DirectoryValue(Get<TextBox>("Destination").Text,false);Get<TextBox>("Destination").Text=preferences.OutputDirectory;return SavePreferences(notify);}catch(Exception e){Get<TextBlock>("Summary").Text=e.Message;if(notify)Message(e.Message);return false;}}
 string UniqueLyric(string name){var matches=lyrics.Where(l=>System.IO.Path.GetFileNameWithoutExtension(l).Equals(name,StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();return matches.Count==1?matches[0]:null;}
 void Pair(){foreach(var s in songs.Where(s=>s.Format!="LRC")){if(s.Lyric==null){string local=System.IO.Path.ChangeExtension(s.Path,".lrc");s.Lyric=File.Exists(local)?local:UniqueLyric(s.Name);}s.Changed("LyricState");}foreach(var s in songs.Where(s=>s.Format=="LRC").ToList())if(songs.Any(a=>a.Lyric==s.Path))songs.Remove(s);Update();}
 void Add(IEnumerable<string> paths){if(busy)return;foreach(var p in paths){try{if(Directory.Exists(p)){Add(Directory.GetFiles(p));foreach(var dir in Directory.GetDirectories(p))if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)==0)Add(new[]{dir});continue;}string ext=System.IO.Path.GetExtension(p).ToLowerInvariant();if(!new[]{".ncm",".flac",".lrc",".mp3"}.Contains(ext))continue;if(ext==".lrc"&&!lyrics.Contains(p,StringComparer.OrdinalIgnoreCase))lyrics.Add(p);if(!songs.Any(s=>s.Path.Equals(p,StringComparison.OrdinalIgnoreCase)))songs.Add(new Song{Path=p});}catch{Get<TextBlock>("Summary").Text="部分文件无法读取，已跳过";}}Pair();}
 string Folder(string initial){using(var d=new Forms.FolderBrowserDialog()){d.Description="选择文件夹";if(Directory.Exists(initial))d.SelectedPath=initial;return d.ShowDialog(new OwnerHandle(w))==Forms.DialogResult.OK?d.SelectedPath:null;}}
 class OwnerHandle:Forms.IWin32Window {Window window;public OwnerHandle(Window w){window=w;}public IntPtr Handle{get{return new System.Windows.Interop.WindowInteropHelper(window).Handle;}}}
 void Settings(){
  Window dialog;using(var f=File.OpenRead(System.IO.Path.Combine(Engine.Base,"src","Settings.xaml")))dialog=(Window)XamlReader.Load(f);dialog.Owner=w;dialog.Resources=w.Resources;
  var input=(TextBox)dialog.FindName("DefaultInput");var output=(TextBox)dialog.FindName("DefaultOutput");var validation=(TextBlock)dialog.FindName("Validation");input.Text=preferences.InputDirectory;output.Text=Get<TextBox>("Destination").Text;
  ((Button)dialog.FindName("PickInput")).Click+=(s,e)=>{var f=Folder(Directory.Exists(input.Text)?input.Text:InputFolder());if(f!=null)input.Text=f;};
  ((Button)dialog.FindName("PickOutput")).Click+=(s,e)=>{var f=Folder(output.Text);if(f!=null)output.Text=f;};
  ((Button)dialog.FindName("ResetPaths")).Click+=(s,e)=>{input.Text="";output.Text="";validation.Text="点击保存后恢复默认目录。";};
  ((Button)dialog.FindName("SaveSettings")).Click+=(s,e)=>{try{string a=PreferenceStore.DirectoryValue(input.Text,true),b=PreferenceStore.DirectoryValue(output.Text,false);var next=new Preferences{InputDirectory=a,OutputDirectory=b,Theme=preferences.Theme,Quality=preferences.Quality};PreferenceStore.Save(next,PreferenceStore.DefaultPath);preferences=next;Get<TextBox>("Destination").Text=b;RefreshHint();Get<TextBlock>("Summary").Text="默认目录已保存";dialog.DialogResult=true;}catch(Exception error){validation.Text=error.Message;}};
  dialog.ShowDialog();
 }
 void Enable(bool enabled){foreach(string n in new[]{"Add","Folder","Remove","Clear","Match","Browse","Settings"})Get<Button>(n).IsEnabled=enabled;Get<ComboBox>("Quality").IsEnabled=enabled;Get<TextBox>("Destination").IsEnabled=enabled;Get<Button>("Cancel").IsEnabled=!enabled;busy=!enabled;Update();}
 async void Start(){
  string dest;try{dest=PreferenceStore.DirectoryValue(Get<TextBox>("Destination").Text,false);}catch(Exception e){Message(e.Message);return;}SaveOutput(false);
  if(!File.Exists(Engine.Ffmpeg)){Message("缺少 bin/ffmpeg.exe，请重新解压完整工具包。");return;}
  var todo=songs.Where(s=>s.Format!="LRC"&&!s.Status.StartsWith("已完成")).ToList();if(todo.Count==0){Message("队列歌曲已完成，可以添加新文件。");return;}
  int quality=new[]{320,256,192,128}[Get<ComboBox>("Quality").SelectedIndex];Enable(false);cts=new CancellationTokenSource();var token=cts.Token;int done=0,failed=0;
  try{for(int i=0;i<todo.Count;i++){if(token.IsCancellationRequested)break;var song=todo[i];int index=i;try{song.Status="正在处理…";string output=await Task.Run(()=>Engine.ConvertSong(song,dest,quality,token,p=>Ui(()=>{Get<ProgressBar>("Progress").Value=100*(index+p)/todo.Count;song.Status=p<.3?"解析 NCM…":"正在转换 "+Math.Min(100,(int)((p-.3)/.7*100))+"%";})));song.Output=output;song.Status="已完成";lastFolder=System.IO.Path.GetDirectoryName(output);done++;Get<Button>("Open").IsEnabled=true;}catch(OperationCanceledException){song.Status="已取消，可重新开始";break;}catch(Exception e){song.Status="失败："+e.Message;failed++;}Get<ProgressBar>("Progress").Value=100.0*(i+1)/todo.Count;Get<TextBlock>("Summary").Text="已完成 "+done+" / "+todo.Count+" · 失败 "+failed;}}
  finally{Get<TextBlock>("Summary").Text=(token.IsCancellationRequested?"已取消 · ":"")+"完成 "+done+" 首，失败 "+failed+" 首";cts.Dispose();cts=null;Enable(true);Get<Button>("Start").Content=failed>0?"重试未完成":"开始转换";}
 }
 void Init(){
  loading=true;preferences=PreferenceStore.Load(PreferenceStore.DefaultPath);
  using(var f=File.OpenRead(System.IO.Path.Combine(Engine.Base,"src","Main.xaml")))w=(Window)XamlReader.Load(f);Looks.Attach(w,preferences.Theme);
  Get<ComboBox>("ThemeChoice").SelectedIndex=preferences.Theme;Get<ComboBox>("Quality").SelectedIndex=preferences.Quality;Get<TextBox>("Destination").Text=preferences.OutputDirectory;RefreshHint();
  if(PreferenceStore.Warning!=null)Get<TextBlock>("Summary").Text=PreferenceStore.Warning;
  Get<DataGrid>("Queue").ItemsSource=songs;Get<DataGrid>("Queue").SizeChanged+=(s,e)=>{var q=Get<DataGrid>("Queue");q.Columns[0].Width=new DataGridLength(Math.Max(140,q.ActualWidth-482));};
  Get<Button>("Settings").Click+=(s,e)=>Settings();
  Get<ComboBox>("ThemeChoice").SelectionChanged+=(s,e)=>{if(loading)return;preferences.Theme=Get<ComboBox>("ThemeChoice").SelectedIndex;Looks.Apply(w,preferences.Theme);SavePreferences(true);};
  Get<ComboBox>("Quality").SelectionChanged+=(s,e)=>{if(loading)return;preferences.Quality=Get<ComboBox>("Quality").SelectedIndex;SavePreferences(true);};
  Get<TextBox>("Destination").LostKeyboardFocus+=(s,e)=>{if(!loading)SaveOutput(false);};
  Get<Button>("Add").Click+=(s,e)=>{var d=new Microsoft.Win32.OpenFileDialog{Multiselect=true,InitialDirectory=InputFolder(),Filter="音乐与歌词|*.ncm;*.flac;*.lrc;*.mp3"};if(d.ShowDialog(w)==true)Add(d.FileNames);};
  Get<Button>("Folder").Click+=(s,e)=>{var f=Folder(InputFolder());if(f!=null)Add(new[]{f});};
  Get<Button>("Browse").Click+=(s,e)=>{var f=Folder(Get<TextBox>("Destination").Text);if(f!=null){Get<TextBox>("Destination").Text=f;SaveOutput(true);}};
  Get<Button>("Clear").Click+=(s,e)=>{songs.Clear();lyrics.Clear();Update();};
  Get<Button>("Remove").Click+=(s,e)=>{foreach(var a in Get<DataGrid>("Queue").SelectedItems.Cast<Song>().ToList()){songs.Remove(a);if(a.Format=="LRC")lyrics.Remove(a.Path);}Update();};
  Get<Button>("Match").Click+=(s,e)=>{var a=Get<DataGrid>("Queue").SelectedItem as Song;if(a==null||a.Format=="LRC"){Message("请先选中需要关联歌词的音频。");return;}var d=new Microsoft.Win32.OpenFileDialog{InitialDirectory=InputFolder(),Filter="歌词文件|*.lrc"};if(d.ShowDialog(w)==true){a.Lyric=d.FileName;Pair();}};
  Get<Button>("Start").Click+=(s,e)=>Start();Get<Button>("Cancel").Click+=(s,e)=>{if(cts!=null)cts.Cancel();};
  Get<Button>("Open").Click+=(s,e)=>{if(lastFolder!=null)Process.Start("explorer.exe","\""+lastFolder+"\"");};
  w.DragOver+=(s,e)=>{e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};w.Drop+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))Add((string[])e.Data.GetData(DataFormats.FileDrop));};
  w.Closing+=(s,e)=>{if(busy){e.Cancel=true;Message("请先取消转换，待任务停止后再关闭。");return;}if(smoke==null)SaveOutput(true);};
  Get<DataGrid>("Queue").LoadingRow+=(s,e)=>{e.Row.ToolTip=e.Row.Item;};Get<DataGrid>("Queue").MouseDoubleClick+=(s,e)=>{var a=Get<DataGrid>("Queue").SelectedItem as Song;if(a!=null&&a.Status.StartsWith("失败"))Message(a.Status);};
  loading=false;
  if(smoke!=null)w.ContentRendered+=(s,e)=>{w.Dispatcher.BeginInvoke(new Action(()=>{File.WriteAllText(smoke,w.Title+"\n"+w.ActualWidth+" x "+w.ActualHeight+"\n"+Get<TextBox>("Destination").Text+"\n"+Get<ComboBox>("ThemeChoice").SelectedIndex+"\n"+InputFolder());w.Close();}));};
  new Application().Run(w);
 }
 [STAThread]public static int Main(string[] args){try{if(args.Length>1&&args[0]=="--ui-smoke")smoke=args[1];if(args.Length>0&&args[0]=="--convert"){var s=new Song{Path=System.IO.Path.GetFullPath(args[1])};string l=System.IO.Path.ChangeExtension(s.Path,".lrc");if(File.Exists(l))s.Lyric=l;Console.WriteLine(Engine.ConvertSong(s,args.Length>2?args[2]:"",320,CancellationToken.None,p=>{}));return 0;}new App().Init();return 0;}catch(Exception e){if(args.Length>0)Console.Error.WriteLine(e);else MessageBox.Show(e.ToString(),"轻转启动失败");return 1;}}
}
