using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
public static class Log {static readonly object gate=new object();public static string Root=AppDomain.CurrentDomain.BaseDirectory;public static void Write(string text){lock(gate){try{Directory.CreateDirectory(Path.Combine(Root,"data"));File.AppendAllText(Path.Combine(Root,"data","client.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch{}}}}
public class ClientForm:PreviewForm {
 Bridge bridge;InputCapture input;System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer();bool busy;DateTime nextLog;int? desiredDirection;
 string config=Path.Combine(Log.Root,"camera-settings.xml");
 public ClientForm():this(true){}
 public ClientForm(bool captureInput){
  input=new InputCapture(captureInput){GameFocused=()=>bridge!=null&&Native.Foreground(bridge.Game.Id),CameraEnabled=()=>bridge!=null&&(bridge.Last.State==2||bridge.Last.State==3),CameraUnlocked=()=>bridge==null||bridge.Last.State!=3,Hotkey=Hotkey};ApplyKeys();
  if(!string.IsNullOrEmpty(loadError))SetStatus(loadError);else if(!string.IsNullOrEmpty(settings.UpgradeNote))SetStatus(settings.UpgradeNote);
  tick.Interval=8;tick.Tick+=(s,e)=>Poll();tick.Start();
  FormClosing+=(s,e)=>{tick.Stop();try{if(bridge!=null)bridge.Dispose();}finally{input.Dispose();tick.Dispose();}};
 }
 public static string RunUiTests(){
  int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
  string temp=Path.Combine(Path.GetTempPath(),"DS3CameraFinal-"+Guid.NewGuid()+".xml");
  try{using(var form=new ClientForm(false)){
   form.config=temp;form.settings=new Settings();form.ApplyKeys();form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
   form.ShowPage(2);form.numbers["相机移动速度"].Value=4.25m;form.numbers["旋转速度"].Value=1.75m;form.numbers["鼠标灵敏度"].Value=.00325m;form.numbers["平滑响应"].Value=.18m;form.numbers["按住加速倍率"].Value=9;form.numbers["战斗减速倍率"].Value=.35m;form.OnSave();
   var saved=Settings.Load(temp);check(saved.MovementSpeed==4.25&&saved.RotationSpeed==1.75&&saved.MouseSensitivity==.00325&&saved.Smoothing==.18&&saved.FastMultiplier==9&&saved.SlowMultiplier==.35,"six speed fields persist from final UI");
   form.ShowPage(1);int old=form.settings.Bindings[3].Key;bool rejected=false;try{form.CommitKey(3,123);}catch(InvalidDataException){rejected=true;}check(rejected&&form.settings.Bindings[3].Key==old,"F12 rejected without losing previous binding");
   rejected=false;try{form.CommitKey(3,45);}catch(InvalidDataException){rejected=true;}check(rejected&&Settings.Load(temp).Bindings[3].Key==old,"duplicate binding leaves disk intact");
   form.CommitKey(3,122);check(Settings.Load(temp).Bindings[3].Key==122&&form.keyGrid.Rows[3].Cells[1].Value.ToString()=="F11"&&form.input.Shortcuts.Contains(122)&&!form.input.Shortcuts.Contains(old),"rebinding updates file, grid and active input routing");
   form.ShowPage(3);form.framing.Checked=false;form.OnSave();check(!Settings.Load(temp).PortraitKeepVerticalFraming,"portrait checkbox persists");
   form.follow.Checked=true;form.OnSave();check(Settings.Load(temp).FollowCameraDirection,"movement-follow option persists");form.follow.Checked=false;form.OnSave();check(!Settings.Load(temp).FollowCameraDirection,"movement-follow option can be switched back off");
   form.ShowPage(2);check(form.numbers["按住加速倍率"].Value==9,"saved settings survive navigation");form.ShowPage(0);check(!form.Controls.OfType<CheckBox>().Any(),"connection has no offline confirmation gate");
   var host=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--test-host"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
   try{
    form.bridge=new Bridge(host);form.bridge.Inject(Path.Combine(Log.Root,"DS3CameraCore.dll"),true);
    Action<Func<bool>> wait=condition=>{var watch=Stopwatch.StartNew();do{form.Poll();Application.DoEvents();if(condition())return;Thread.Sleep(10);}while(watch.ElapsedMilliseconds<5000);throw new Exception("final UI runtime timeout: "+form.status.Text);};
    wait(()=>form.bridge.Last.State==1);check(form.status.Text.Contains("已连接"),"live connected status");
    form.OnCommand(1);wait(()=>form.bridge.Last.State==2);check(form.status.Text.Contains("相机开启"),"UI camera command reaches DLL");
    wait(()=>Math.Abs(form.bridge.Last.Speed-4.25)<.001&&Math.Abs(form.bridge.Last.FastMultiplier-9)<.001&&Math.Abs(form.bridge.Last.SlowMultiplier-.35)<.001);check(true,"saved UI values reach live IPC");
    form.ShowPage(3);form.OnDirection(2);wait(()=>form.bridge.Last.OutputRotation==-90&&form.desiredDirection==null);check(form.direction.SelectedIndex==2,"direct counterclockwise selection reaches DLL");
    form.OnDirection(1);wait(()=>form.bridge.Last.OutputRotation==90&&form.desiredDirection==null);check(form.direction.SelectedIndex==1,"direct clockwise selection reaches DLL");
    form.OnCommand(1024);wait(()=>(form.bridge.Last.EffectFlags&4)!=0);check(true,"UI slow-time command");
    form.OnCommand(512);wait(()=>(form.bridge.Last.EffectFlags&2)!=0);check(true,"UI pause command");
    form.OnCommand(256);wait(()=>(form.bridge.Last.EffectFlags&1)!=0);check(true,"UI hide command");
    form.OnDisconnect();check(form.bridge==null&&form.status.Text=="未连接","disconnect releases frontend session");
   }finally{try{if(!host.HasExited)host.Kill();host.WaitForExit(3000);}finally{host.Dispose();}}
   form.Close();
  }}finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(temp+".bak"))File.Delete(temp+".bak");}
  return checks+" final UI/configuration/live DLL fixture checks passed. No game accessed; user configuration unchanged.\r\n";
 }
 void ApplyKeys(){input.Shortcuts=new HashSet<int>(settings.Bindings.Where(b=>Settings.Shortcut(b.Action)).Select(b=>b.Key));input.Continuous=new HashSet<int>(settings.Bindings.Where(b=>!Settings.Shortcut(b.Action)).Select(b=>b.Key));}
 protected override void OnRefresh(){if(processField==null)return;var games=Process.GetProcessesByName("DarkSoulsIII");try{processField.Text=games.Length==1?"DarkSoulsIII.exe  ·  PID "+games[0].Id:games.Length==0?"尚未检测到游戏进程":"检测到多个游戏进程，请保留一个";}finally{foreach(var game in games)game.Dispose();}if(injectButton!=null)injectButton.Enabled=!busy;}
 protected override void OnDisconnect(){desiredDirection=null;if(bridge!=null){bridge.Dispose();bridge=null;}input.Clear();SetStatus("未连接");}
 protected override void OnCommand(uint command){Command(command);}
 protected override void OnDirection(int index){if(bridge==null){SetStatus("请先连接游戏");return;}desiredDirection=index==0?0:index==1?90:-90;}
 protected override void OnSave(){Save();}
 protected override void OnCaptureKey(){CaptureKey();}
 protected override void OnInject(){Inject();}
 void Hotkey(int key,bool ctrl){if(InvokeRequired){BeginInvoke((Action)(()=>Hotkey(key,ctrl)));return;}var binding=settings.Bindings.FirstOrDefault(b=>b.Key==key);if(binding==null)return;uint command=Settings.Command(binding.Action);if(command!=0){if(command==1||command==16||command==128)desiredDirection=null;Command(command);}}
 void Command(uint command){try{if(bridge!=null&&!busy)bridge.Command(command);}catch(Exception e){Log.Write(e.ToString());SetStatus(e.Message);}}
 internal void CommitKey(int index,int key){int previous=settings.Bindings[index].Key;settings.Bindings[index].Key=key;try{settings.Validate();settings.Save(config);}catch{settings.Bindings[index].Key=previous;throw;}ApplyKeys();input.Clear();RefreshKeys();SetStatus("按键已保存并应用");}
 void CaptureKey(){if(keyGrid==null||keyGrid.CurrentRow==null)return;int selectedKey=keyGrid.CurrentRow.Index;using(var dialog=new KeyDialog()){if(dialog.ShowDialog(this)!=DialogResult.OK)return;CommitKey(selectedKey,dialog.Key);}}
 void Save(){ReadSettingsPage();settings.Validate();settings.Save(config);loadError="";ApplyKeys();input.Clear();SetStatus("设置已保存并应用");}
 async void Inject(){
  if(busy)return;
  var games=Process.GetProcessesByName("DarkSoulsIII");if(games.Length!=1){MessageBox.Show(this,"请先启动唯一一个魂3进程并进入场景。");return;}
  busy=true;desiredDirection=null;if(injectButton!=null)injectButton.Enabled=false;
  try{if(bridge!=null)bridge.Dispose();bridge=new Bridge(games[0]);Bridge target=bridge;await System.Threading.Tasks.Task.Run(()=>target.Inject(Path.Combine(Log.Root,"DS3CameraCore.dll")));SetStatus("已加载，等待相机初始化");Log.Write("Injected DLL into DS3 pid="+games[0].Id);}
  catch(Exception e){Log.Write(e.ToString());if(bridge!=null){bridge.Dispose();bridge=null;}MessageBox.Show(this,e.Message,"注入失败");}
  finally{busy=false;if(injectButton!=null)injectButton.Enabled=true;}
 }
 double Axis(string positive,string negative){return(input.Down(settings.Key(positive))?1:0)-(input.Down(settings.Key(negative))?1:0);}
 void Poll(){
  if(bridge==null||busy)return;
  try{
   if(bridge.Game.HasExited){bridge.Dispose();bridge=null;desiredDirection=null;SetStatus("游戏已退出");return;}
   V3 mouse=input.MouseDelta();bool focus=Native.Foreground(bridge.Game.Id);
   bridge.Update(p=>{p.Heartbeat=Win.GetTickCount64();p.Focused=focus?1u:0u;p.MoveX=focus?(float)Axis("右移","左移"):0;p.MoveY=focus?(float)Axis("世界上升","世界下降"):0;p.MoveZ=focus?(float)Axis("前进","后退"):0;
    p.TurnX=focus?(float)Axis("向下转","向上转"):0;p.TurnY=focus?(float)Axis("向右转","向左转"):0;p.TurnZ=focus?(float)Axis("左倾斜","右倾斜"):0;p.Zoom=focus?(float)Axis("增大FOV","缩小FOV"):0;
    p.MouseX+=(float)mouse.X;p.MouseY+=(float)mouse.Y;p.Wheel+=(float)mouse.Z;p.Speed=(float)settings.MovementSpeed;p.TurnSpeed=(float)settings.RotationSpeed;p.Sensitivity=(float)settings.MouseSensitivity;p.Smoothing=(float)settings.Smoothing;p.FastMultiplier=(float)settings.FastMultiplier;p.Fast=focus&&input.Down(settings.Key("按住加速"))?1u:0u;p.ScreenControls=1;p.KeepFraming=settings.PortraitKeepVerticalFraming?1u:0u;p.SlowMultiplier=(float)settings.SlowMultiplier;p.Commands|=settings.FollowCameraDirection?2048u:4096u;return p;});
   Packet current=bridge.Last;
   if(desiredDirection.HasValue){if(current.State==2||current.State==3){if(current.OutputRotation==desiredDirection.Value)desiredDirection=null;else if((current.Commands&128)==0)bridge.Command(128);}else{desiredDirection=null;}}
   if(direction!=null&&!desiredDirection.HasValue)direction.SelectedIndex=current.OutputRotation==90?1:current.OutputRotation==-90?2:0;
   string label=current.State==0?"已断开 / 等待初始化":current.State==1?"已连接，按相机开关键开启":current.State==2?"相机开启":current.State==3?"相机锁定":"DLL拒绝接管（错误 "+current.Error+"）";
   if(current.Error==5)label+="：镜头函数被改动、版本不符或钩子初始化失败；退出其他镜头工具后重启游戏。";
   if(current.FeatureError!=0)label+=" | 功能错误 "+current.FeatureError+(current.FeatureError==12?"（暂停不可用）":current.FeatureError==13?"（减速状态冲突）":"（UI对象变化）");
   SetStatus(label);
   if(DateTime.UtcNow>=nextLog){nextLog=DateTime.UtcNow.AddSeconds(2);Log.Write("STATE="+current.State+" ERROR="+current.Error+" frames="+current.Frames+" notice="+current.Notice+" xyz="+current.X+","+current.Y+","+current.Z+" fov="+current.Fov+" output="+current.OutputRotation+" effects="+current.EffectFlags+" feature-error="+current.FeatureError+" render-fov="+current.RenderFov+" time-scale="+current.TimeScale);}
  }catch(Exception e){Log.Write(e.ToString());SetStatus(e.Message);}
 }
}
public static class UiCheck {
 public static void Run(){using(var form=new ClientForm(false)){form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();Directory.CreateDirectory(Path.Combine(Log.Root,"preview"));for(int i=0;i<6;i++){form.ShowPage(i);Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Log.Root,"preview","final-"+i+".png"));}}form.Close();}}
}
public class KeyDialog:Form {
 public int Key;public KeyDialog(){Text="录入按键";ClientSize=new Size(420,110);StartPosition=FormStartPosition.CenterParent;Controls.Add(new Label{Text="请按一个按键；Esc 取消。小键盘键请打开 NumLock 后录入。",Dock=DockStyle.Fill,Padding=new Padding(15)});}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){int raw=(int)keyData&255;if(raw==27){DialogResult=DialogResult.Cancel;return true;}if((keyData&(Keys.Control|Keys.Shift|Keys.Alt))!=0||raw==16||raw==17||raw==18)return true;int flags=((msg.LParam.ToInt64()>>24)&1)!=0?1:0;Key=InputCapture.NormalizeKey(raw,flags);DialogResult=DialogResult.OK;return true;}
}
public static class Program {
 [STAThread]public static int Main(string[] args){
  if(args.Contains("--test-host")){Thread.Sleep(60000);return 0;}
  if(args.Contains("--ui-self-test")){PreviewDpi.Enable();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{File.WriteAllText(Path.Combine(Log.Root,"data","ui-self-test.txt"),ClientForm.RunUiTests());return 0;}catch(Exception e){File.WriteAllText(Path.Combine(Log.Root,"data","ui-self-test.txt"),e.ToString());return 1;}}
  if(args.Contains("--rotation-test")){try{Directory.CreateDirectory(Path.Combine(Log.Root,"data"));File.WriteAllText(Path.Combine(Log.Root,"data","rotation-test.txt"),Tests.RunRotation());return 0;}catch(Exception e){File.WriteAllText(Path.Combine(Log.Root,"data","rotation-test.txt"),e.ToString());return 1;}}
  if(args.Contains("--self-test")){try{Directory.CreateDirectory(Path.Combine(Log.Root,"data"));File.WriteAllText(Path.Combine(Log.Root,"data","self-test.txt"),Tests.Run());return 0;}catch(Exception e){File.WriteAllText(Path.Combine(Log.Root,"data","self-test.txt"),e.ToString());return 1;}}
  PreviewDpi.Enable();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Contains("--preview-render")){PreviewForm.Render();return 0;}if(args.Contains("--preview")){Application.Run(new PreviewForm());return 0;}if(args.Contains("--ui-check")){UiCheck.Run();return 0;}try{Application.Run(new ClientForm());return 0;}catch(Exception e){Log.Write(e.ToString());MessageBox.Show(e.Message);return 1;}
 }
}
}
