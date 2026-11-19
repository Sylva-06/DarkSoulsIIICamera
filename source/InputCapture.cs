using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DS3CameraDLLTrial {
// A separate message-pump thread keeps game hotkeys independent of the settings UI.
public sealed class InputCapture:IDisposable {
    public const int CutsceneShortcut=768,SlowerShortcut=769,FasterShortcut=770;
    public Func<bool> GameFocused,CameraEnabled,CameraUnlocked;
    public Action<int,bool> Hotkey;
    readonly object sync=new object();readonly HashSet<int> keys=new HashSet<int>();
    readonly HashSet<int> swallowed=new HashSet<int>();
    readonly ManualResetEvent ready=new ManualResetEvent(false);
    Thread thread;uint threadId;IntPtr keyboard,mouse;Native.HookProc keyboardProc,mouseProc;
    Exception failure;InputWindow window;double dx,dy,wheel;long rawPackets;
    public long RawPackets {get{return Interlocked.Read(ref rawPackets);}}
    public HashSet<int> Continuous=new HashSet<int>{37,38,39,40,97,99,100,101,102,103,104,105,107,109};
    public HashSet<int> Shortcuts=new HashSet<int>{34,45,36,46,96,98,106,112,113,114,115,116,117,118,119,121,269};
    public static int NormalizeKey(int key,int flags){
        bool extended=(flags&1)!=0;if(key==13&&extended)return 269;
        if(!extended)switch(key){case 45:return 96;case 35:return 97;case 40:return 98;case 34:return 99;case 37:return 100;case 12:return 101;case 39:return 102;case 36:return 103;case 38:return 104;case 33:return 105;case 46:return 110;}
        return key;
    }
    public InputCapture():this(true){}
    public InputCapture(bool install){
        if(!install)return;
        thread=new Thread(Run){Name="DS3Camera-GameInput",IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!ready.WaitOne(5000))throw new TimeoutException("游戏输入线程启动超时");if(failure!=null)throw new InvalidOperationException("游戏输入监听初始化失败",failure);
    }
    bool Focused(){return GameFocused!=null&&GameFocused();}
    bool Active(){return CameraEnabled!=null&&CameraEnabled();}
    bool Unlocked(){return CameraUnlocked==null||CameraUnlocked();}
    void Run(){try{
        threadId=Native.GetCurrentThreadId();keyboardProc=Keyboard;mouseProc=Mouse;
        window=new InputWindow(this);window.Open();
        Native.RawDevice[] devices={new Native.RawDevice{Page=1,Usage=2,Flags=0x100,Target=window.Handle}};
        if(!Native.RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf(typeof(Native.RawDevice))))throw new Win32Exception();
        keyboard=Native.SetWindowsHookEx(13,keyboardProc,Native.GetModuleHandle(null),0);mouse=Native.SetWindowsHookEx(14,mouseProc,Native.GetModuleHandle(null),0);
        if(keyboard==IntPtr.Zero||mouse==IntPtr.Zero)throw new Win32Exception();
        ready.Set();Application.Run();
    }catch(Exception e){failure=e;ready.Set();Log.Write("input thread: "+e);}
    finally{if(keyboard!=IntPtr.Zero)Native.UnhookWindowsHookEx(keyboard);if(mouse!=IntPtr.Zero)Native.UnhookWindowsHookEx(mouse);if(window!=null)window.Close();}}
    // Exposed for deterministic pipeline tests; no operating-system key injection is used.
    public bool ProcessKey(int raw,int flags,bool down,bool focused,bool active){
        int key=NormalizeKey(raw,flags);bool ctrl,was,blocked;
        bool alt=(flags&0x20)!=0;
        int shortcut=key;
        bool isShortcut=Shortcuts.Contains(shortcut);
        lock(sync){
            was=keys.Contains(key);ctrl=keys.Contains(17)||keys.Contains(162)||keys.Contains(163);
            if(down&&focused)keys.Add(key);if(!down)keys.Remove(key);
            blocked=focused&&(isShortcut||(active&&Continuous.Contains(key)));
            if(down&&blocked)swallowed.Add(key);
            if(!down){blocked=swallowed.Remove(key)||blocked;}
        }
        if(down&&!was&&focused&&isShortcut&&Hotkey!=null)Hotkey(shortcut,ctrl);
        return blocked;
    }
    IntPtr Keyboard(int n,IntPtr w,IntPtr l){
        if(n>=0)try{int msg=w.ToInt32();if(msg==0x100||msg==0x104||msg==0x101||msg==0x105){if(ProcessKey(Marshal.ReadInt32(l),Marshal.ReadInt32(l,8),msg==0x100||msg==0x104,Focused(),Active()))return new IntPtr(1);}}
        catch(Exception e){Log.Write("keyboard: "+e.Message);}
        return Native.CallNextHookEx(keyboard,n,w,l);
    }
    IntPtr Mouse(int n,IntPtr w,IntPtr l){
        if(n>=0&&w.ToInt32()==0x20a&&Focused()&&Active()&&Unlocked()){
            int data=Marshal.ReadInt32(l,8);lock(sync)wheel+=(short)(data>>16)/120.0;return new IntPtr(1);
        }return Native.CallNextHookEx(mouse,n,w,l);
    }
    void RawMouse(IntPtr handle){
        if(!Focused()||!Active()||!Unlocked())return;
        uint size=0;const uint header=24;
        if(Native.GetRawInputData(handle,0x10000003,IntPtr.Zero,ref size,header)==uint.MaxValue||size<header+24||size>65536)return;
        IntPtr packet=Marshal.AllocHGlobal((int)size);try{
            if(Native.GetRawInputData(handle,0x10000003,packet,ref size,header)!=size||Marshal.ReadInt32(packet)!=0)return;
            int flags=(ushort)Marshal.ReadInt16(packet,24);if((flags&1)!=0)return;
            Interlocked.Increment(ref rawPackets);AddRawDelta(Marshal.ReadInt32(packet,36),Marshal.ReadInt32(packet,40));
        }finally{Marshal.FreeHGlobal(packet);}
    }
    public void AddRawDelta(int x,int y){lock(sync){dx+=x;dy+=y;}}
    public bool Down(int key){lock(sync)return keys.Contains(key);}
    public V3 MouseDelta(){lock(sync){V3 d=new V3(dx,dy,wheel);dx=dy=wheel=0;return d;}}
    public void Clear(){lock(sync){keys.Clear();swallowed.Clear();dx=dy=wheel=0;}}
    public void Dispose(){if(thread!=null&&thread.IsAlive){Native.PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero);if(Thread.CurrentThread!=thread)thread.Join(2000);}ready.Dispose();}
    sealed class InputWindow:NativeWindow {
        InputCapture owner;public InputWindow(InputCapture o){owner=o;}
        public void Open(){CreateHandle(new CreateParams{Caption="DS3Camera.RawInput",Parent=new IntPtr(-3)});}
        public void Close(){DestroyHandle();}
        protected override void WndProc(ref Message m){if(m.Msg==0xff){try{owner.RawMouse(m.LParam);}catch(Exception e){Log.Write("raw mouse: "+e.Message);}}base.WndProc(ref m);}
    }
}
}
