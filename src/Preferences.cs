using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
 public static string DefaultPath { get { return Path.Combine(Engine.Base,"settings.json"); } }
 public static Preferences Load(string path) {
  Warning=null;
  try { if(!File.Exists(path))return new Preferences();var p=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path,Encoding.UTF8));if(p==null)throw new InvalidDataException();p.InputDirectory=p.InputDirectory??"";p.OutputDirectory=p.OutputDirectory??"";if(p.Theme<0||p.Theme>3)p.Theme=0;if(p.Quality<0||p.Quality>3)p.Quality=0;return p; }
  catch(Exception) { Warning="设置文件无法读取，已使用默认设置。";return new Preferences(); }
 }
 public static void Save(Preferences p,string path) {
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
 public static readonly string[] Names={"极简亮色","霓虹夜色","酸性青柠","糖果撞色"};
 public static readonly string[] Keys={"Bg","Surface","Ink","Muted","Line","Accent","OnAccent","Hero","HeroInk","Selection","SelectedInk"};
 public static readonly string[][] Colors={
  new[]{"#F7F8FA","#FFFFFF","#182232","#596577","#7D8BA0","#2563EB","#FFFFFF","#EFF5FF","#182232","#DBEAFE","#182232"},
  new[]{"#0C0A18","#171329","#F7F3FF","#C3B7D7","#726484","#D4A0FF","#170A27","#291446","#FAEFFF","#49305F","#FFFFFF"},
  new[]{"#11140B","#1C2114","#F2F6E8","#BCC7A7","#73805D","#D5FF3F","#192000","#D5FF3F","#192000","#3A4819","#F3FFD9"},
  new[]{"#FFF1F6","#FFFFFF","#321443","#73516E","#92708B","#8427B7","#FFFFFF","#FFC6E5","#321443","#EED5FA","#321443"}
 };
 public static readonly string[] Headlines={"让音乐，随处可听。","把热爱，调到最大。","DROP IT.  PLAY IT.","好音乐，不设限。"};
 public static SolidColorBrush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
 public static void Attach(Window window,int theme){using(var f=File.OpenRead(Path.Combine(Engine.Base,"src","Theme.xaml")))window.Resources=(ResourceDictionary)XamlReader.Load(f);Apply(window,theme);}
 public static void Apply(Window window,int theme){theme=Math.Max(0,Math.Min(3,theme));for(int i=0;i<Keys.Length;i++)window.Resources[Keys[i]]=Brush(Colors[theme][i]);
  var title=window.FindName("HeroTitle")as TextBlock;if(title!=null){title.Text=Headlines[theme];title.FontFamily=new FontFamily(theme==2?"Segoe UI Black, Microsoft YaHei UI":"Microsoft YaHei UI");title.FontSize=theme==2?32:29;}
  var hero=window.FindName("HeroPanel")as Border;if(hero!=null){hero.CornerRadius=new CornerRadius(theme==2?0:theme==3?22:12);hero.BorderThickness=new Thickness(theme==2||theme==3?2:1);hero.Effect=theme==1?new DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString(Colors[theme][5]),BlurRadius=20,ShadowDepth=0,Opacity=.22}:theme==3?new DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString("#321443"),BlurRadius=0,ShadowDepth=5,Direction=315,Opacity=1}:null;}
 }
}
