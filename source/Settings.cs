using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
namespace DS3CameraDLLTrial {
public class Binding {public string Action;public int Key;public Binding(){}public Binding(string action,int key){Action=action;Key=key;}}
public class Settings {
 public bool PortraitKeepVerticalFraming=true;
 public bool FollowCameraDirection=false;
 [XmlIgnore]public string UpgradeNote="";
 public double MovementSpeed=3,RotationSpeed=1.3,MouseSensitivity=.002,Smoothing=.12,FastMultiplier=5,SlowMultiplier=.25;
 public Binding[] Bindings=new Binding[]{new Binding("相机开关",45),new Binding("锁定相机",36),new Binding("回到人物附近",116),new Binding("倾斜复位",98),new Binding("FOV复位",106),new Binding("前进",104),new Binding("后退",101),new Binding("左移",100),new Binding("右移",102),new Binding("世界上升",103),new Binding("世界下降",105),new Binding("左倾斜",97),new Binding("右倾斜",99),new Binding("向上转",38),new Binding("向下转",40),new Binding("向左转",37),new Binding("向右转",39),new Binding("缩小FOV",109),new Binding("增大FOV",107),new Binding("按住加速",110),new Binding("隐藏或显示UI",46),new Binding("暂停或继续",96),new Binding("切换竖屏",117),new Binding("减缓时间或恢复",120)};
 public static uint Command(string action){switch(action){case "相机开关":return 1;case "锁定相机":return 2;case "倾斜复位":return 4;case "FOV复位":return 8;case "回到人物附近":return 16;case "切换竖屏":return 128;case "隐藏或显示UI":return 256;case "暂停或继续":return 512;case "减缓时间或恢复":return 1024;default:return 0;}}
 public int Key(string action){return Bindings.First(x=>x.Action==action).Key;}
 public static bool Shortcut(string action){return action=="相机开关"||action=="锁定相机"||action=="回到人物附近"||action=="倾斜复位"||action=="FOV复位"||action=="隐藏或显示UI"||action=="暂停或继续"||action=="切换竖屏"||action=="减缓时间或恢复";}
 public void Validate(){
  double[] values={MovementSpeed,RotationSpeed,MouseSensitivity,Smoothing,FastMultiplier,SlowMultiplier};foreach(double v in values)if(double.IsNaN(v)||double.IsInfinity(v))throw new InvalidDataException("设置必须是有效数字");
  if(MovementSpeed<.01||MovementSpeed>100||RotationSpeed<.01||RotationSpeed>10||MouseSensitivity<.00001||MouseSensitivity>.05||Smoothing<0||Smoothing>1||FastMultiplier<1||FastMultiplier>50||SlowMultiplier<.05||SlowMultiplier>1)throw new InvalidDataException("速度或灵敏度超出允许范围");
  string[] actions=new Settings().Bindings.Select(x=>x.Action).OrderBy(x=>x).ToArray();
  if(Bindings==null||Bindings.Any(x=>x==null)||!Bindings.Select(x=>x.Action).OrderBy(x=>x).SequenceEqual(actions))throw new InvalidDataException("按键配置不完整或存在未知操作");
  var used=new HashSet<int>();foreach(Binding b in Bindings){if(b.Key<8||b.Key>254||b.Key==123||b.Key==16||b.Key==17||b.Key==18||b.Key>=160&&b.Key<=165)throw new InvalidDataException(b.Action+"：请选择单独按键，F12 保留给截图");if(!used.Add(b.Key))throw new InvalidDataException("按键冲突："+Name(b.Key));}
 }
 public static string Name(int key){if(key>=96&&key<=105)return "小键盘 "+(key-96);switch(key){case 110:return "小键盘 .";case 106:return "小键盘 *";case 107:return "小键盘 +";case 109:return "小键盘 -";default:return ((Keys)key).ToString();}}
 public void Save(string path){Validate();string temp=path+".tmp";using(var file=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(file,this);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
 public static Settings Load(string path){if(!File.Exists(path))return new Settings();if(new FileInfo(path).Length>65536)throw new InvalidDataException("配置文件过大");var options=new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null};using(var reader=System.Xml.XmlReader.Create(path,options)){var settings=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);if(settings.Bindings!=null){var list=settings.Bindings.Where(b=>b==null||b.Action!="切换控制方向").ToList();foreach(var binding in new Settings().Bindings)if(!list.Any(b=>b!=null&&b.Action==binding.Action)){int requested=binding.Key;if(list.Any(b=>b!=null&&b.Key==binding.Key)){binding.Key=new[]{120,122,33,145,19,111}.Concat(Enumerable.Range(8,247)).First(k=>k!=123&&k!=16&&k!=17&&k!=18&&!(k>=160&&k<=165)&&!list.Any(b=>b!=null&&b.Key==k));settings.UpgradeNote+="新增操作“"+binding.Action+"”与旧按键冲突，已分配为 "+Name(binding.Key)+"。\n";}list.Add(binding);}settings.Bindings=list.ToArray();}settings.Validate();return settings;}}
}
}

