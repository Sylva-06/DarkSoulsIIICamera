using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
public static class Tests {
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int NativeTest();
 static int count;
 static void Check(bool ok,string message){count++;if(!ok)throw new Exception("check failed: "+message);}
 static Packet Wait(Bridge bridge,Func<Packet,bool> condition,int timeout=5000,bool heartbeat=true){var watch=Stopwatch.StartNew();do{bridge.Update(p=>{if(heartbeat)p.Heartbeat=Win.GetTickCount64();return p;});if(condition(bridge.Last))return bridge.Last;Thread.Sleep(10);}while(watch.ElapsedMilliseconds<timeout);throw new Exception("DLL timeout: state="+bridge.Last.State+" error="+bridge.Last.Error+" ack="+bridge.Last.Ack);}
 static Packet Move(Bridge bridge,bool fast,int ms){var watch=Stopwatch.StartNew();do{bridge.Update(p=>{p.Heartbeat=Win.GetTickCount64();p.Focused=1;p.MoveZ=1;p.Fast=fast?1u:0u;p.Smoothing=0;return p;});Thread.Sleep(8);}while(watch.ElapsedMilliseconds<ms);return bridge.Last;}
 public static string RunRotation(){
  IntPtr module=Win.LoadLibraryW(Path.Combine(Log.Root,"DS3CameraCore.dll"));if(module==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
  var test=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(module,"DS3Camera_RunRotationTests"),typeof(NativeTest));int result=test();
  if(result<=0)throw new Exception("Rotation regression failed: "+result);
  return result+" native rotation scenarios passed: mouse/arrows, no roll drift, preserved manual bank, explicit roll/reset, pole limits and screen turn direction.\r\n21,600 simulated update frames checked. No game process accessed.\r\n";
 }
 public static string Run(){
  Check(Marshal.SizeOf(typeof(Packet))==168,"managed protocol byte layout");
  IntPtr dll=Win.LoadLibraryW(Path.Combine(Log.Root,"DS3CameraCore.dll"));if(dll==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
  var size=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(dll,"DS3Camera_ProtocolSize"),typeof(NativeTest));Check(size()==168,"native and managed packet match");
  var native=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(dll,"DS3Camera_RunMathTests"),typeof(NativeTest));int nativeCount=native();Check(nativeCount>80,"native pose, speed, elevation, validation and live MinHook enable/restore: "+nativeCount);
  var stage2=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(dll,"DS3Camera_RunStage2Tests"),typeof(NativeTest));int stage2Count=stage2();Check(stage2Count>150,"native stage-two projection/UI/pause/cleanup: "+stage2Count);
  var slowTest=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(dll,"DS3Camera_RunSlowTimeTests"),typeof(NativeTest));int slowCount=slowTest();Check(slowCount>30,"native slowdown/pause/ownership/restore: "+slowCount);
  var rotation=(NativeTest)Marshal.GetDelegateForFunctionPointer(Win.GetProcAddress(dll,"DS3Camera_RunRotationTests"),typeof(NativeTest));Check(rotation()==25,"confirmed no-roll-drift behaviour retained");
  var settings=new Settings();settings.Validate();Check(settings.Key("按住加速")==110&&settings.FastMultiplier==5,"decimal five-times acceleration default");Check(settings.Key("回到人物附近")==116,"F5 recenter default");Check(settings.Key("减缓时间或恢复")==120&&settings.SlowMultiplier==.25&&!settings.Bindings.Any(b=>b.Key==121),"F9 slowdown default and no F10 binding");
  Check(settings.Key("隐藏或显示UI")==46&&settings.Key("暂停或继续")==96&&settings.Key("切换竖屏")==117,"stage-two default keys");
  Check(Settings.Command("隐藏或显示UI")==256&&Settings.Command("暂停或继续")==512&&Settings.Command("切换竖屏")==128,"stage-two command routing");
  string path=Path.Combine(Path.GetTempPath(),"DS3CameraSettings-"+Guid.NewGuid()+".xml");
  try{settings.MovementSpeed=4;settings.FastMultiplier=7;settings.Bindings.First(b=>b.Action=="按住加速").Key=111;settings.Save(path);var copy=Settings.Load(path);Check(copy.MovementSpeed==4&&copy.FastMultiplier==7&&copy.Key("按住加速")==111,"config roundtrip without recompilation");copy.Bindings[1].Key=copy.Bindings[0].Key;bool rejected=false;try{copy.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"conflicting keys rejected");copy=new Settings();copy.Bindings[0].Key=123;rejected=false;try{copy.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"F12 reserved");}
  finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".bak"))File.Delete(path+".bak");}
  string legacyPath=Path.Combine(Path.GetTempPath(),"DS3CameraLegacy-"+Guid.NewGuid()+".xml");
  try{var legacy=new Settings();legacy.Bindings=legacy.Bindings.Where(b=>b.Action!="隐藏或显示UI"&&b.Action!="暂停或继续"&&b.Action!="切换竖屏"&&b.Action!="减缓时间或恢复").Concat(new[]{new Binding("切换控制方向",121)}).ToArray();legacy.MovementSpeed=4.2;legacy.FastMultiplier=7;legacy.Bindings.First(b=>b.Action=="按住加速").Key=117;using(var file=File.Create(legacyPath))new System.Xml.Serialization.XmlSerializer(typeof(Settings)).Serialize(file,legacy);var migrated=Settings.Load(legacyPath);Check(migrated.Bindings.Length==24&&migrated.MovementSpeed==4.2&&migrated.FastMultiplier==7&&migrated.Key("按住加速")==117,"stage-one config upgrades without replacing custom keys/speed");Check(!migrated.Bindings.Any(b=>b.Action=="切换控制方向")&&migrated.SlowMultiplier==.25,"legacy F10 action removed and slowdown setting added");Check(migrated.Key("切换竖屏")!=117&&!string.IsNullOrEmpty(migrated.UpgradeNote),"new-key conflicts resolved while preserving existing binding");}
  finally{if(File.Exists(legacyPath))File.Delete(legacyPath);}
  using(var input=new InputCapture(false)){
   var events=new List<int>();input.Shortcuts=new HashSet<int>{45,116};input.Continuous=new HashSet<int>{110};input.Hotkey=(key,ctrl)=>events.Add(key);
   Check(input.ProcessKey(46,0,true,true,true)&&input.Down(110)&&events.Count==0,"NumLock-off decimal is acceleration, not Delete");input.ProcessKey(46,0,false,true,true);
   Check(!input.ProcessKey(46,1,true,true,true)&&events.Count==0,"dedicated Delete untouched in stage one");input.ProcessKey(46,1,false,true,true);
   input.Shortcuts=new HashSet<int>(settings.Bindings.Where(b=>Settings.Shortcut(b.Action)).Select(b=>b.Key));input.ProcessKey(46,1,true,true,true);Check(events.Last()==46,"Delete UI dispatch");input.ProcessKey(46,1,false,true,true);input.ProcessKey(45,0,true,true,true);Check(events.Last()==96,"NumLock-off Num0 pauses rather than toggles camera");input.ProcessKey(45,0,false,true,true);input.ProcessKey(117,0,true,true,true);Check(events.Last()==117,"F6 portrait dispatch");input.ProcessKey(117,0,false,true,true);Check(!input.ProcessKey(121,0,true,true,true),"F10 passes through after removal");input.ProcessKey(121,0,false,true,true);input.ProcessKey(120,0,true,true,true);Check(events.Last()==120,"F9 slowdown dispatch");input.ProcessKey(120,0,false,true,true);
   Check(!input.ProcessKey(123,0,true,true,true),"F12 passthrough");input.ProcessKey(123,0,false,true,true);
   input.ProcessKey(116,0,true,true,true);Check(events.Last()==116,"F5 dispatch");input.ProcessKey(116,0,false,true,true);
   input.Shortcuts=new HashSet<int>{120};Check(!input.ProcessKey(45,1,true,true,true),"old key stops dispatching after remap");input.ProcessKey(45,1,false,true,true);input.ProcessKey(120,0,true,true,true);Check(events.Last()==120,"new key dispatches without recompilation");
  }
  var host=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--test-host"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
  try{
   using(var bridge=new Bridge(host)){
    bool duplicate=false;try{using(var second=new Bridge(host)){}}catch(InvalidOperationException){duplicate=true;}Check(duplicate,"second client cannot take ownership of existing session");
    bridge.Inject(Path.Combine(Log.Root,"DS3CameraCore.dll"),true);Wait(bridge,s=>s.State==1);Check(true,"real remote LoadLibrary injection into own test host");
    bridge.Command(1);Packet p=Wait(bridge,s=>s.State==2);Check(Math.Abs(p.X-10)<.01&&Math.Abs(p.Z-20)<.01,"activate from live camera arguments");
    float start=p.Z;p=Move(bridge,false,300);double normal=p.Z-start;float next=p.Z;p=Move(bridge,true,300);double fast=p.Z-next;Check(normal>.3&&fast/normal>3.5&&fast/normal<6.5,"held acceleration changes native translation");
    bridge.Update(s=>{s.MoveZ=0;s.Fast=0;s.MouseX=100;return s;});Wait(bridge,s=>s.Frames>p.Frames+3);bridge.Command(16);p=Wait(bridge,s=>s.Notice==2&&Math.Abs(s.Z-20)<.01);Check(p.State==2&&Math.Abs(p.X-10)<.01,"F5 uses fresh current follow pose and keeps camera on");
    bridge.Command(2);Wait(bridge,s=>s.State==3);Check(true,"lock in DLL");bridge.Command(16);Wait(bridge,s=>s.State==2);Check(true,"F5 unlocks while recentering");
    bridge.Command(128);p=Wait(bridge,s=>s.OutputRotation==90);Check(p.RenderFov<p.Fov,"F6 composes portrait projection");
    bridge.Command(128);Wait(bridge,s=>s.OutputRotation==-90);bridge.Command(128);Wait(bridge,s=>s.OutputRotation==0);Check(true,"F6 completes cycle without pose reset");
    bridge.Command(1024);p=Wait(bridge,s=>(s.EffectFlags&4)!=0&&s.TimeScale==.25f);float slowStart=p.Z;p=Move(bridge,false,300);Check((p.Z-slowStart)/normal>.65&&(p.Z-slowStart)/normal<1.35,"game slowdown does not slow native camera movement");bridge.Update(s=>{s.MoveZ=0;return s;});bridge.Command(512);Wait(bridge,s=>(s.EffectFlags&2)!=0&&s.TimeScale==0);bridge.Command(512);Wait(bridge,s=>(s.EffectFlags&2)==0&&s.TimeScale==.25f);Check(true,"slowdown-pause-resume retains configured slowdown");bridge.Command(1024);Wait(bridge,s=>s.TimeScale==1&&s.EffectFlags==0);
    bridge.Command(256);Wait(bridge,s=>(s.EffectFlags&1)!=0);bridge.Command(512);Wait(bridge,s=>s.EffectFlags==3);Check(true,"HUD and pause can be active together");
    bridge.Command(1);Wait(bridge,s=>s.State==1&&s.EffectFlags==0);Check(true,"Insert closes camera and restores UI/pause");
    bridge.Command(256|512|1024);Wait(bridge,s=>s.EffectFlags==7);
    Wait(bridge,s=>s.State==0&&s.EffectFlags==0&&s.TimeScale==1,4000,false);Check(true,"heartbeat loss restores and deactivates hook");
    bridge.Command(64);Wait(bridge,s=>s.State==1);Check(true,"reattach dormant DLL without reinjection");
    bridge.Command(256|512);Wait(bridge,s=>s.EffectFlags==3);bridge.Command(32);Wait(bridge,s=>s.State==0&&s.EffectFlags==0);Check(true,"explicit disconnect cleanup");
   }
  }finally{if(!host.HasExited)host.Kill();host.Dispose();}
  string config=Path.Combine(Log.Root,"camera-settings.xml");Settings.Load(config).Save(config);
  return count+" managed configuration/input/IPC/injection/combined cleanup checks passed.\r\n"+stage2Count+" native portrait/HUD/menu/pause/restore checks passed.\r\n"+slowCount+" native slowdown/pause/ownership/restore checks passed.\r\n25 no-roll-drift rotation scenarios retained.\r\n"+nativeCount+" native math/acceleration/MinHook restore checks passed.\r\nAll injection and hook execution used this tool's own test processes; no game process was accessed.\r\nActual game rendering, current-player F5 framing and mod compatibility require manual testing.\r\n";
 }
}
}

