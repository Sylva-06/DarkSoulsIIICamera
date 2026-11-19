using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace DS3CameraDLLTrial {
[StructLayout(LayoutKind.Sequential,Pack=8)]public struct Packet {
 public uint Magic,Version,ClientPid;public float TimeScale;public ulong Heartbeat;
 public uint Commands,Focused;
 public float MoveX,MoveY,MoveZ,TurnX,TurnY,TurnZ,MouseX,MouseY,Wheel,Speed,TurnSpeed,Sensitivity,Smoothing,FastMultiplier;
 public uint Fast,State,Ack,Error;public ulong Frames;public float X,Y,Z,Fov;public uint Notice;public float Zoom;
 public uint ScreenControls,KeepFraming;public int OutputRotation;public uint EffectFlags,FeatureError;public float Aspect,RenderFov;public float SlowMultiplier;
}
public static class Win {
 [DllImport("kernel32.dll")]public static extern ulong GetTickCount64();
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr LoadLibraryW(string path);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)]public static extern IntPtr GetProcAddress(IntPtr module,string proc);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern IntPtr CreateRemoteThread(IntPtr process,IntPtr attributes,UIntPtr stack,IntPtr entry,IntPtr parameter,uint flags,out uint id);
 [DllImport("kernel32.dll")]public static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
}
public sealed class Bridge:IDisposable {
 public readonly Process Game;MemoryMappedFile map;MemoryMappedViewAccessor view;Mutex mutex,lease;public Packet Last;bool disposed;
 public Bridge(Process game){Game=game;if(Marshal.SizeOf(typeof(Packet))!=168)throw new InvalidOperationException("通信结构大小不符");string name="Local\\DS3CameraDLLv3-"+game.Id;
  try{bool created;lease=new Mutex(false,name+"-Client",out created);if(!created)throw new InvalidOperationException("已有另一个 DLL 相机客户端连接该游戏，请先关闭它。");mutex=new Mutex(false,name+"-Lock");map=MemoryMappedFile.CreateOrOpen(name,4096,MemoryMappedFileAccess.ReadWrite);view=map.CreateViewAccessor();Update(p=>new Packet{Magic=0x33434d44,Version=3,TimeScale=1,ClientPid=(uint)Process.GetCurrentProcess().Id,Heartbeat=Win.GetTickCount64(),Speed=3,TurnSpeed=1.3f,Sensitivity=.002f,Smoothing=.12f,FastMultiplier=5,ScreenControls=1,KeepFraming=1,SlowMultiplier=.25f,Commands=64});}
  catch{if(view!=null)view.Dispose();if(map!=null)map.Dispose();if(mutex!=null)mutex.Dispose();if(lease!=null)lease.Dispose();throw;}
 }
 public void Update(Func<Packet,Packet> change){if(disposed)return;bool acquired=false;try{try{acquired=mutex.WaitOne(100);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new IOException("DLL 通信暂忙");Packet p;view.Read(0,out p);p=change(p);view.Write(0,ref p);Last=p;}finally{if(acquired)mutex.ReleaseMutex();}}
 public void Command(uint command){Update(p=>{p.Commands|=command;p.Heartbeat=Win.GetTickCount64();return p;});}
 public void Inject(string path,bool test=false){
  path=Path.GetFullPath(path);if(!File.Exists(path))throw new FileNotFoundException("找不到相机 DLL",path);
  if(!test){if(!Game.ProcessName.Equals("DarkSoulsIII",StringComparison.OrdinalIgnoreCase)||Game.MainModule.FileVersionInfo.FileVersion!="1.15.2.0")throw new InvalidOperationException("仅支持 DarkSoulsIII.exe 1.15.2.0");}
  else if(Game.ProcessName!="DS3CameraClient")throw new InvalidOperationException("测试只能注入本工具的专用测试进程");
  Game.Refresh();if(Game.Modules.Cast<ProcessModule>().Any(m=>m.ModuleName.Equals("DS3CameraCore.dll",StringComparison.OrdinalIgnoreCase)&&!string.Equals(m.FileName,path,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("游戏仍加载了其他版本相机DLL，请退出游戏和旧客户端后再更新。");if(Game.Modules.Cast<ProcessModule>().Any(m=>string.Equals(m.FileName,path,StringComparison.OrdinalIgnoreCase))){Command(64);return;}
  IntPtr process=Native.OpenProcess(0x43a,false,Game.Id);if(process==IntPtr.Zero)throw new Win32Exception();IntPtr remote=IntPtr.Zero,thread=IntPtr.Zero;bool complete=false;
  try{
   byte[] data=Encoding.Unicode.GetBytes(path+"\0");remote=Native.VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)data.Length,0x3000,0x04);if(remote==IntPtr.Zero)throw new Win32Exception();IntPtr done;
   if(!Native.WriteProcessMemory(process,remote,data,data.Length,out done)||done.ToInt64()!=data.Length)throw new Win32Exception();
   IntPtr local=Win.GetProcAddress(Native.GetModuleHandle("kernel32.dll"),"LoadLibraryW");if(local==IntPtr.Zero)throw new Win32Exception();long address=local.ToInt64();
   ProcessModule localOwner=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(m=>address>=m.BaseAddress.ToInt64()&&address<m.BaseAddress.ToInt64()+m.ModuleMemorySize);
   ProcessModule remoteOwner=Game.Modules.Cast<ProcessModule>().First(m=>m.ModuleName.Equals(localOwner.ModuleName,StringComparison.OrdinalIgnoreCase));
   IntPtr entry=new IntPtr(remoteOwner.BaseAddress.ToInt64()+address-localOwner.BaseAddress.ToInt64());uint id;
   thread=Win.CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,entry,remote,0,out id);if(thread==IntPtr.Zero)throw new Win32Exception();
   if(Win.WaitForSingleObject(thread,15000)!=0)throw new IOException("DLL 加载超时；请退出游戏后重试，勿重复注入");complete=true;Game.Refresh();
   if(!Game.Modules.Cast<ProcessModule>().Any(m=>string.Equals(m.FileName,path,StringComparison.OrdinalIgnoreCase)))throw new IOException("DLL 未能加载，请检查进程权限和 DLL 文件");
  }finally{if(thread!=IntPtr.Zero)Native.CloseHandle(thread);if(remote!=IntPtr.Zero&&(complete||thread==IntPtr.Zero))Native.VirtualFreeEx(process,remote,UIntPtr.Zero,0x8000);Native.CloseHandle(process);}
 }
 public void Disconnect(){if(disposed)return;Command(32);}
 public void Dispose(){if(disposed)return;try{Disconnect();}catch{}disposed=true;if(view!=null)view.Dispose();if(map!=null)map.Dispose();if(mutex!=null)mutex.Dispose();if(lease!=null)lease.Dispose();}
}
}
