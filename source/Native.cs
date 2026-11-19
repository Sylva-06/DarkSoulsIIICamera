using System;
using System.Runtime.InteropServices;
namespace DS3CameraDLLTrial {
public static class Native {
    [DllImport("kernel32.dll",SetLastError=true)]public static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,int n,out IntPtr done);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,int n,out IntPtr done);
    [DllImport("kernel32.dll")]public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protect,out uint old);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint allocation,uint protect);
    [DllImport("kernel32.dll")]public static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr n,uint operation);
    [DllImport("kernel32.dll")]public static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
    [DllImport("ntdll.dll")]public static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")]public static extern int NtResumeProcess(IntPtr h);
    [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]public static extern IntPtr SetWindowsHookEx(int id,HookProc p,IntPtr module,uint tid);
    [DllImport("user32.dll")]public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")]public static extern IntPtr CallNextHookEx(IntPtr h,int n,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")]public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",SetLastError=true)]public static extern bool PostThreadMessage(uint tid,uint msg,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)]public struct RawDevice {public ushort Page,Usage;public uint Flags;public IntPtr Target;}
    [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]public static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
    public delegate IntPtr HookProc(int n,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)]public struct POINT {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]public struct MouseData {public POINT Point;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]public struct Pad {public uint Packet;public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
    [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")]static extern uint Pad14(uint id,out Pad p);
    [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")]static extern uint Pad13(uint id,out Pad p);
    public static bool TryPad(out Pad p){for(uint i=0;i<4;i++){try{if(Pad14(i,out p)==0)return true;}catch(DllNotFoundException){try{if(Pad13(i,out p)==0)return true;}catch(DllNotFoundException){break;}}}p=new Pad();return false;}
    public static bool Foreground(int pid){uint actual;GetWindowThreadProcessId(GetForegroundWindow(),out actual);return actual==(uint)pid;}
}
public struct V3 {public double X,Y,Z;public V3(double x,double y,double z){X=x;Y=y;Z=z;}}
}
