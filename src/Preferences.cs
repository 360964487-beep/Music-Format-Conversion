using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Web.Script.Serialization;

public class Preferences {
 public string InputDirectory { get; set; }
 public string OutputDirectory { get; set; }
 public int Theme { get; set; }
 public int Quality { get; set; }
 public Preferences() { InputDirectory="";OutputDirectory="";Theme=0;Quality=0; }
}
public static class PreferenceStore {
 public static string Warning;
 public static string DefaultPath {
  get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QingZhuan","settings.json"); }
 }
 public static Preferences LoadCurrent(string userPath) {
  string portablePath=Path.Combine(Engine.Base,"settings.json");
  return Load(File.Exists(userPath)?userPath:portablePath);
 }
 public static Preferences Load(string path) {
  Warning=null;
  try { if(!File.Exists(path))return new Preferences();var p=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path,Encoding.UTF8));if(p==null)throw new InvalidDataException();p.InputDirectory=p.InputDirectory??"";p.OutputDirectory=p.OutputDirectory??"";if(p.Theme<0||p.Theme>=Looks.Names.Length)p.Theme=0;if(p.Quality<0||p.Quality>3)p.Quality=0;return p; }
  catch(Exception) { Warning="设置文件无法读取，已使用默认设置。";return new Preferences(); }
 }
 public static void Save(Preferences p,string path) {
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try { File.WriteAllText(temp,new JavaScriptSerializer().Serialize(p),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }
  finally { if(File.Exists(temp))File.Delete(temp); }
 }
 public static string DirectoryValue(string value,bool mustExist) {
  value=(value??"").Trim();if(value.Length==0)return "";
  if(value.IndexOfAny(Path.GetInvalidPathChars())>=0||value.IndexOf('"')>=0||!Path.IsPathRooted(value)||(!value.StartsWith(@"\\")&&(value.Length<3||value[1]!=':'||value[2]!='\\')))throw new ArgumentException("请使用完整目录路径，或点击“选择”。");
  value=Path.GetFullPath(value);if(File.Exists(value))throw new ArgumentException("该路径是文件，请选择文件夹。");if(mustExist&&!Directory.Exists(value))throw new ArgumentException("添加文件目录不存在，请重新选择。");return value;
 }
}
public static class Looks {
 public static void FitQueue(DataGrid queue) {
  double available=queue.ActualWidth-SystemParameters.VerticalScrollBarWidth-4;
  for(int i=1;i<queue.Columns.Count;i++)available-=queue.Columns[i].Width.Value;
  queue.Columns[0].Width=new DataGridLength(Math.Max(queue.Columns[0].MinWidth,available));
 }
 public static readonly string[] Names={"极简亮色","霓虹夜色","酸性青柠","糖果撞色","电光蓝橙","像素终端","奶油唱片"};
 public static readonly string[] Keys={"Bg","Surface","Ink","Muted","Line","Accent","OnAccent","Hero","HeroInk","Selection","SelectedInk"};
 public static readonly string[][] Colors={
  new[]{"#F7F8FA","#FFFFFF","#182232","#596577","#7D8BA0","#2563EB","#FFFFFF","#EFF5FF","#182232","#DBEAFE","#182232"},
  new[]{"#0C0A18","#171329","#F7F3FF","#C3B7D7","#726484","#D4A0FF","#170A27","#291446","#FAEFFF","#49305F","#FFFFFF"},
  new[]{"#11140B","#1C2114","#F2F6E8","#BCC7A7","#73805D","#D5FF3F","#192000","#D5FF3F","#192000","#3A4819","#F3FFD9"},
  new[]{"#FFF1F6","#FFFFFF","#321443","#73516E","#92708B","#8427B7","#FFFFFF","#FFC6E5","#321443","#EED5FA","#321443"},
  new[]{"#071B46","#122B62","#F6F8FF","#BCCEFF","#8098D6","#FFAD58","#271200","#2446A0","#FFFFFF","#375CC6","#FFFFFF"},
  new[]{"#07120E","#0D2017","#ECFFF4","#A8CBB5","#58816A","#43F5A4","#052619","#153C29","#E6FFF0","#24593E","#F0FFF5"},
  new[]{"#FFF3D8","#FFFDF5","#352218","#6F5041","#977363","#A12D42","#FFFFFF","#F6D9A8","#421E27","#F3CFB6","#352218"}
 };
 public static SolidColorBrush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
 public static void Attach(Window window,int theme){using(var f=File.OpenRead(Path.Combine(Engine.Base,"src","Theme.xaml")))window.Resources=(ResourceDictionary)XamlReader.Load(f);Apply(window,theme);}
 public static void Apply(Window window,int theme){theme=Math.Max(0,Math.Min(Names.Length-1,theme));for(int i=0;i<Keys.Length;i++)window.Resources[Keys[i]]=Brush(Colors[theme][i]);
 }
}
