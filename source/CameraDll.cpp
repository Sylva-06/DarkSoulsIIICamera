#include "CameraCore.h"
#include "Effects.h"
#include "vendor/minhook/include/MinHook.h"
#include <cstdio>
#include <cwchar>
#include <winver.h>
#include <atomic>

using namespace camera;
namespace {
HMODULE module;Packet* ipc;HANDLE mapping,ipcMutex;SRWLOCK gate=SRWLOCK_INIT;
Effects effects;std::atomic<bool> restoreRequested{false};Frame lastTemplate;bool hasTemplate=false;
Motion motion;bool armed=false,pendingToggle=false,pendingReset=false,testMode=false;
uint64_t frameCount=0;std::atomic<uint32_t> errorCode{0},notice{0};uintptr_t boundOwner=0,boundPlayer=0;
uintptr_t imageBase,hookSite;bool hookEnabled=false;LARGE_INTEGER lastClock{},frequency{};
using CopyCamera=void(__fastcall*)(void*,void*,void*);CopyCamera original;
bool readMemory(uintptr_t address,void* out,size_t count){SIZE_T n=0;return address>=0x10000&&ReadProcessMemory(GetCurrentProcess(),(void*)address,out,count,&n)&&n==count;}
uintptr_t pointer(uintptr_t address){uintptr_t x=0;readMemory(address,&x,8);return x;}
void stopMotion(){AcquireSRWLockExclusive(&gate);motion.on=false;motion.locked=false;motion.outputDegrees=0;motion.clear();pendingToggle=pendingReset=false;boundOwner=boundPlayer=0;ReleaseSRWLockExclusive(&gate);}
void __fastcall cameraCopy(void* owner,void* primary,void* secondary){
 Frame frame;bool replace=false;uintptr_t expected=testMode?(uintptr_t)owner:pointer(pointer(imageBase+0x475abd0)+0x18);
 uintptr_t player=testMode?1:pointer(pointer(imageBase+0x477fdb8)+0x80);
 AcquireSRWLockExclusive(&gate);
 if(armed&&(uintptr_t)owner==expected&&expected&&player){
  frameCount++;
  Frame incoming;if(readMemory((uintptr_t)primary,&incoming,sizeof(incoming))){lastTemplate=incoming;hasTemplate=true;float aspect=getFloat(incoming,0x54);if(std::isfinite(aspect)&&aspect>=.2f&&aspect<=8)motion.aspect=aspect;}
  if(motion.on&&(boundOwner!=expected||boundPlayer!=player)){motion.on=false;motion.clear();restoreRequested=true;notice=3;}
  if(pendingToggle&&motion.on&&!pendingReset){motion.on=false;motion.clear();motion.outputDegrees=0;restoreRequested=true;notice=0;pendingToggle=false;}
  if(pendingToggle||pendingReset){
   Frame live;Pose pose;
   if(readMemory((uintptr_t)primary,&live,sizeof(live))&&decode(live,pose)){
    errorCode=0;motion.reset(pose);motion.on=true;boundOwner=expected;boundPlayer=player;notice=pendingReset?2:1;
   }else{errorCode=4;}
   pendingToggle=pendingReset=false;
  }
  if(motion.on&&readMemory((uintptr_t)primary,&frame,sizeof(frame))){encode(frame,motion.rendered());replace=true;}
 }else if(armed&&motion.on&&(!expected||!player)){motion.on=false;motion.clear();restoreRequested=true;notice=3;}
 ReleaseSRWLockExclusive(&gate);
 if(replace)original(owner,&frame,&frame);else original(owner,primary,secondary);
}
void disableHook(){stopMotion();effects.restore();AcquireSRWLockExclusive(&gate);armed=false;ReleaseSRWLockExclusive(&gate);if(hookEnabled&&!testMode){if(MH_DisableHook((void*)hookSite)!=MH_OK){errorCode=7;return;}}hookEnabled=false;}
bool enableHook(){
 if(hookEnabled)return true;
 if(!testMode){const unsigned char expected[]={0x48,0x89,0x5c,0x24,0x08,0x48,0x89,0x6c,0x24,0x10,0x48,0x89,0x74,0x24,0x18,0x57};unsigned char actual[16];if(!readMemory(hookSite,actual,16)||memcmp(expected,actual,16)){errorCode=5;return false;}}
 if(!testMode&&MH_EnableHook((void*)hookSite)!=MH_OK){errorCode=6;return false;}
 hookEnabled=true;AcquireSRWLockExclusive(&gate);armed=true;ReleaseSRWLockExclusive(&gate);errorCode=0;notice=0;return true;
}
bool gameVersion(){
 wchar_t path[MAX_PATH];GetModuleFileNameW(nullptr,path,MAX_PATH);const wchar_t* name=wcsrchr(path,L'\\');name=name?name+1:path;
 if(!_wcsicmp(name,L"DS3CameraClient.exe")&&wcsstr(GetCommandLineW(),L"--test-host")){testMode=true;return true;}
 if(_wcsicmp(name,L"DarkSoulsIII.exe"))return false;
 DWORD unused,size=GetFileVersionInfoSizeW(path,&unused);if(!size||size>65536)return false;
 auto data=(unsigned char*)HeapAlloc(GetProcessHeap(),0,size);if(!data)return false;bool ok=false;
 if(GetFileVersionInfoW(path,0,size,data)){VS_FIXEDFILEINFO* info=nullptr;UINT n=0;if(VerQueryValueW(data,L"\\",(void**)&info,&n)&&n>=sizeof(*info))ok=info->dwFileVersionMS==MAKELONG(15,1)&&info->dwFileVersionLS==MAKELONG(0,2);}
 HeapFree(GetProcessHeap(),0,data);return ok;
}
void __fastcall mockOriginal(void*,void*,void*){}
void mockFrame(){Frame live;Pose p;p.position={10,2,20};encode(live,p);putFloat(live,0x54,16.f/9);cameraCopy((void*)1,&live,&live);}
DWORD WINAPI run(void*){
 if(!gameVersion())return 1;HMODULE pinned;GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN|GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,reinterpret_cast<LPCWSTR>(&run),&pinned);imageBase=(uintptr_t)GetModuleHandleW(nullptr);hookSite=imageBase+0x62dcb0;
 wchar_t name[100];swprintf(name,100,L"Local\\DS3CameraDLLv3-%lu",GetCurrentProcessId());
 for(int retry=0;retry<100&&!mapping;retry++){mapping=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,name);if(!mapping)Sleep(50);}if(!mapping)return 2;
 ipc=(Packet*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Packet));if(!ipc)return 3;
 wcscat(name,L"-Lock");ipcMutex=OpenMutexW(SYNCHRONIZE|MUTEX_MODIFY_STATE,FALSE,name);if(!ipcMutex)return 4;
 const unsigned char expected[]={0x48,0x89,0x5c,0x24,0x08,0x48,0x89,0x6c,0x24,0x10,0x48,0x89,0x74,0x24,0x18,0x57};unsigned char actual[16];
 effects.configure(imageBase);effects.mock=testMode;
 if(testMode)original=mockOriginal;
 else if(!readMemory(hookSite,actual,16)||memcmp(expected,actual,16)||MH_Initialize()!=MH_OK||MH_CreateHook((void*)hookSite,(void*)cameraCopy,(void**)&original)!=MH_OK)errorCode=5;
 QueryPerformanceFrequency(&frequency);QueryPerformanceCounter(&lastClock);uint32_t lastClient=0;
 for(;;){
  Packet input{};bool got=false;DWORD wait=WaitForSingleObject(ipcMutex,20);
  if(wait==WAIT_OBJECT_0||wait==WAIT_ABANDONED){if(ipc->magic==Magic&&ipc->version==Version){input=*ipc;ipc->commands=0;ipc->mouseX=ipc->mouseY=ipc->wheel=0;got=true;}ReleaseMutex(ipcMutex);}
  LARGE_INTEGER now;QueryPerformanceCounter(&now);double dt=(double)(now.QuadPart-lastClock.QuadPart)/frequency.QuadPart;lastClock=now;
  bool healthy=got&&input.clientPid&&GetTickCount64()>=input.heartbeat&&GetTickCount64()-input.heartbeat<2000;
  if(restoreRequested.exchange(false))effects.restore();
  if(!hookEnabled&&effects.flags())effects.restore();
  if(!testMode){uintptr_t currentOwner=pointer(pointer(imageBase+0x475abd0)+0x18),currentPlayer=pointer(pointer(imageBase+0x477fdb8)+0x80);AcquireSRWLockShared(&gate);bool changed=motion.on&&boundOwner&&(boundOwner!=currentOwner||boundPlayer!=currentPlayer);ReleaseSRWLockShared(&gate);if(changed||(effects.flags()&&(!currentOwner||!currentPlayer))){stopMotion();effects.restore();notice=3;}}
  if(!healthy){if(hookEnabled)disableHook();else if(effects.flags())effects.restore();notice=4;}
  else if(errorCode!=5){
   if(input.commands&Disconnect){disableHook();lastClient=input.clientPid;}
   else{
    if((input.commands&Attach)||input.clientPid!=lastClient){disableHook();enableHook();lastClient=input.clientPid;}
    if(hookEnabled&&validConfig(input)){
     if(errorCode==8)errorCode=0;
     AcquireSRWLockExclusive(&gate);
     motion.screenControls=true;motion.keepFraming=input.keepFraming!=0;
     if(input.commands&Toggle){if(motion.on){motion.on=false;motion.clear();motion.outputDegrees=0;restoreRequested=true;}else pendingToggle=!pendingToggle;}
     if((input.commands&Recenter)&&motion.on){if(effects.paused&&hasTemplate){Pose live;if(decode(lastTemplate,live)){motion.reset(live);notice=2;}}else pendingReset=true;}
     if((input.commands&Portrait)&&motion.on)motion.cyclePortrait();
     if((input.commands&Lock)&&motion.on){motion.locked=!motion.locked;motion.clear();}
     if((input.commands&RollReset)&&motion.on)motion.resetRoll();
     if((input.commands&FovReset)&&motion.on)motion.pose.fov=motion.initialFov;
     motion.update(input,dt);ReleaseSRWLockExclusive(&gate);
     if(restoreRequested.exchange(false))effects.restore();
     if(input.commands&Hud)effects.setHud(!effects.hidden);
     if(input.commands&Pause)effects.setPause(!effects.paused);
     effects.setSlowRatio(input.slowMultiplier);
     if(input.commands&Slow)effects.setSlow(!effects.slow);
     effects.maintain();
     if(testMode)mockFrame();
     if(effects.paused&&!testMode){
      Frame output;uintptr_t owner;bool sync=false;
      AcquireSRWLockShared(&gate);owner=boundOwner;if(motion.on&&hasTemplate){output=lastTemplate;encode(output,motion.rendered());sync=true;}ReleaseSRWLockShared(&gate);
      if(sync&&owner==pointer(pointer(imageBase+0x475abd0)+0x18))for(int offset:{0x10,0x18,0x20}){uintptr_t camera=pointer(owner+offset);if(camera){memoryWrite(camera+0x10,output.bytes+0x10,64);memoryWrite(camera+0x50,output.bytes+0x50,4);}}
     }
    }else if(hookEnabled){stopMotion();errorCode=8;}
   }
  }
  wait=WaitForSingleObject(ipcMutex,20);
  if(wait==WAIT_OBJECT_0||wait==WAIT_ABANDONED){
   AcquireSRWLockShared(&gate);ipc->state=errorCode?5:!hookEnabled?0:motion.on?(motion.locked?3:2):1;ipc->error=errorCode.load();ipc->frames=frameCount;
   ipc->x=(float)motion.pose.position.x;ipc->y=(float)motion.pose.position.y;ipc->z=(float)motion.pose.position.z;ipc->fov=(float)motion.pose.fov;ipc->notice=notice.load();ipc->outputRotation=motion.outputDegrees;ipc->effectFlags=effects.flags();ipc->timeScale=effects.timeScale();ipc->featureError=effects.error;ipc->aspect=(float)motion.aspect;ipc->renderFov=(float)motion.rendered().fov;ipc->ack++;
   ReleaseSRWLockShared(&gate);ReleaseMutex(ipcMutex);
  }
  Sleep(4);
 }
}
}
namespace {using TestFunction=int(*)(int);TestFunction testOriginal;int testDetour(int x){return testOriginal(x)+7;}}
extern "C" __declspec(dllexport) __attribute__((noinline)) int DS3Camera_TestTarget(int x){return x*3+1;}
extern "C" __declspec(dllexport) int DS3Camera_ProtocolSize(){return sizeof(Packet);}
extern "C" __declspec(dllexport) int DS3Camera_RunMathTests(){
 wchar_t executable[MAX_PATH];GetModuleFileNameW(nullptr,executable,MAX_PATH);const wchar_t* file=wcsrchr(executable,L'\\');if(!file||_wcsicmp(file+1,L"DS3CameraClient.exe"))return -999;
 int count=0;auto check=[&](bool ok){if(!ok)return false;count++;return true;};
 for(double yaw:{0.,.7,2.})for(double pitch:{-1.3,0.,1.3})for(double roll:{0.,1.57,-1.57}){
  Pose p;p.position={1,2,3};p.rotation=Quat::axis({0,1,0},yaw)*Quat::axis({1,0,0},pitch)*Quat::axis({0,0,1},roll);Frame f;encode(f,p);Pose result;
  if(!check(decode(f,result)&&(result.position-p.position).length()<1e-6&&(result.rotation.rotate({1,0,0})-p.rotation.rotate({1,0,0})).length()<1e-6))return -count-1;
  Motion normal,fast;normal.on=fast.on=true;normal.reset(p);fast.reset(p);Packet input;input.focused=1;input.moveZ=1;input.mouseX=3;input.wheel=1;input.smoothing=0;
  normal.update(input,.02);input.fast=1;fast.update(input,.02);
  if(!check(std::abs((fast.pose.position-p.position).length()/(normal.pose.position-p.position).length()-5)<1e-6&&fast.pose.fov==normal.pose.fov&&(fast.pose.rotation.rotate({0,0,1})-normal.pose.rotation.rotate({0,0,1})).length()<1e-10))return -count-1;
  normal.reset(p);input.fast=0;input.moveZ=0;input.moveY=1;input.mouseX=input.wheel=0;normal.update(input,.02);
  if(!check(std::abs(normal.pose.position.y-p.position.y-.06)<1e-6&&normal.pose.position.x==p.position.x))return -count-1;
 }
 Packet input;if(!check(validConfig(input)))return -count-1;input.speed=NAN;if(!check(!validConfig(input)))return -count-1;
 if(MH_Initialize()!=MH_OK)return -1001;
 if(MH_CreateHook((void*)DS3Camera_TestTarget,(void*)testDetour,(void**)&testOriginal)!=MH_OK)return -1002;
 volatile TestFunction target=DS3Camera_TestTarget;
 if(!check(target(4)==13))return -1003;
 if(MH_EnableHook((void*)DS3Camera_TestTarget)!=MH_OK||!check(target(4)==20))return -1004;
 if(MH_DisableHook((void*)DS3Camera_TestTarget)!=MH_OK||!check(target(4)==13))return -1005;
 MH_RemoveHook((void*)DS3Camera_TestTarget);MH_Uninitialize();
 return count;
}
extern "C" __declspec(dllexport) int DS3Camera_RunRotationTests(){
 int count=0;
 auto bank=[](const Pose& p){Vec f=p.rotation.rotate({0,0,1}),right=p.rotation.rotate({1,0,0});Vec horizon=cross({0,1,0},f);horizon=horizon*(1/horizon.length());Vec vertical=cross(f,horizon);return std::atan2(dot(right,vertical),dot(right,horizon));};
 for(bool mouse:{false,true})for(double initialPitch:{-1.3,0.,1.3})for(double initialRoll:{0.,.65,-1.2}){
  Motion m;Pose p;p.rotation=Quat::axis({0,1,0},.4)*Quat::axis({1,0,0},initialPitch)*Quat::axis({0,0,1},initialRoll);m.reset(p);m.on=true;
  Packet input;input.focused=1;input.smoothing=.12f;
  for(int step=0;step<1200;step++){
   double horizontal=std::sin(step*.043),vertical=std::cos(step*.071);
   if(mouse){input.mouseX=(float)(horizontal*4);input.mouseY=(float)(vertical*3);}else{input.turnY=(float)horizontal;input.turnX=(float)vertical;}
   m.update(input,.016);
   double difference=std::remainder(bank(m.pose)-initialRoll,2*3.141592653589793);
   if(std::abs(difference)>1e-8)return -100-count;
   Vec f=m.pose.rotation.rotate({0,0,1});if(!std::isfinite(f.y)||std::abs(f.y)>std::sin(89.0*3.141592653589793/180)+1e-10)return -200-count;
  }
  count++;
 }
 Motion m;Pose p;p.rotation=Quat::axis({1,0,0},.8);m.reset(p);m.on=true;Packet input;input.focused=1;input.smoothing=0;input.turnZ=1;
 m.update(input,.05);if(bank(m.pose)<.01)return -301;count++;
 Vec forward=m.pose.rotation.rotate({0,0,1});m.resetRoll();if(std::abs(bank(m.pose))>1e-10||(m.pose.rotation.rotate({0,0,1})-forward).length()>1e-10)return -302;count++;
 input.turnZ=0;input.mouseY=-10000;m.update(input,.016);for(int j=0;j<100;j++)m.update(input,.016);if(std::abs(bank(m.pose))>1e-8)return -303;count++;
 input.mouseY=0;input.mouseX=1;for(int j=0;j<100;j++)m.update(input,.016);if(std::abs(bank(m.pose))>1e-8)return -304;count++;
 // Verify screen-relative turn direction when a manual bank is present.
 for(double tilt:{0.,.7,-1.2}){
  p.rotation=Quat::axis({0,1,0},.4)*Quat::axis({1,0,0},.3)*Quat::axis({0,0,1},tilt);m.reset(p);Vec right=p.rotation.rotate({1,0,0});forward=p.rotation.rotate({0,0,1});input.mouseX=.01f;input.mouseY=0;m.update(input,.016);
  if(dot(m.pose.rotation.rotate({0,0,1})-forward,right)<=0)return -305;count++;
 }
 return count;
}
extern "C" __declspec(dllexport) int DS3Camera_RunStage2Tests(){
 int count=0;auto check=[&](bool ok){if(ok)count++;return ok;};
 for(double aspect:{1.,16./9,21./9})for(double fov:{5.,43.,100.,150.})for(int degrees:{90,-90}){
  Pose p;p.fov=fov;p.rotation=Quat::axis({0,1,0},.4)*Quat::axis({1,0,0},.3);Pose out=portraitPose(p,degrees,aspect,true);
  if(!check(std::abs(2*std::atan(std::tan(out.fov*3.141592653589793/360)*aspect)*180/3.141592653589793-fov)<1e-8))return -10-count;
  if(!check((out.rotation.rotate({0,0,1})-p.rotation.rotate({0,0,1})).length()<1e-10&&p.fov==fov))return -20-count;
  Motion motion;motion.reset(p);motion.on=true;motion.aspect=aspect;motion.outputDegrees=degrees;Packet input;input.focused=1;input.smoothing=0;input.moveX=1;
  Vec expected=out.rotation.rotate({1,0,0});motion.update(input,.02);
  if(!check(((motion.pose.position-p.position)-expected*.06).length()<1e-7))return -30-count;
  double bank=motion.roll;input.moveX=0;input.mouseX=1;for(int j=0;j<80;j++)motion.update(input,.016);
  if(!check(motion.roll==bank&&motion.outputDegrees==degrees))return -40-count;
  motion.resetRoll();if(!check(motion.outputDegrees==degrees))return -50-count;
 }
 Motion cycle;cycle.cyclePortrait();if(!check(cycle.outputDegrees==90))return -101;cycle.cyclePortrait();if(!check(cycle.outputDegrees==-90))return -102;cycle.cyclePortrait();if(!check(cycle.outputDegrees==0))return -103;
 auto allocation=(unsigned char*)VirtualAlloc(nullptr,8192,MEM_COMMIT|MEM_RESERVE,PAGE_EXECUTE_READWRITE);if(!allocation)return -201;
 uintptr_t a=(uintptr_t)allocation;Effects effect;effect.dataSlot=a+0x100;uintptr_t data=a+0x200,options=a+0x400,render=a+0x700;effect.menuSlot=a+0x108;effect.expectedMenuVtable=a+0x1200;
 memoryWrite(effect.dataSlot,&data,8);memoryWrite(data+0x58,&options,8);memoryWrite(effect.menuSlot,&render,8);memoryWrite(render,&effect.expectedMenuVtable,8);
 for(unsigned char initial:{0,1,2}){
  memoryWrite(options+0x11,&initial,1);unsigned char zero=0;memoryWrite(render+0x90,&zero,1);
  for(int loop=0;loop<3;loop++){
   if(!check(effect.setHud(true)&&effect.flags()==1))return -210-count;
   unsigned char hv,mv;memoryRead(options+0x11,&hv,1);memoryRead(render+0x90,&mv,1);if(!check(hv==0&&mv==1))return -220-count;
   unsigned char one=1;memoryWrite(options+0x11,&one,1);memoryWrite(render+0x90,&zero,1);if(!check(effect.maintain()))return -230-count;
   if(!check(effect.setHud(false)))return -240-count;memoryRead(options+0x11,&hv,1);memoryRead(render+0x90,&mv,1);if(!check(hv==(initial==2?2:1)&&mv==0))return -250-count;
  }
 }
 if(MH_Initialize()!=MH_OK)return -300;
 const unsigned char harness[]={0x53,0x48,0x89,0xcb,0x80,0xbb,0x98,0,0,0,0,0x0f,0x94,0xc0,0x0f,0xb6,0xc0,0x5b,0xc3};memcpy(allocation,harness,sizeof(harness));
 effect.pauseSite=a+4;effect.speedAddress=a+0x1500;const unsigned char prefix[]={0xc7,0x83,0x60,0x09,0,0};memcpy((void*)(effect.speedAddress-6),prefix,6);float normal=1;memoryWrite(effect.speedAddress,&normal,4);
 using Compare=int(*)(void*);Compare call=(Compare)allocation;auto character=allocation+0x300;character[0x98]=0;FlushInstructionCache(GetCurrentProcess(),allocation,sizeof(harness));
 if(!check(call(character)==1))return -301;
 if(!check(effect.setPause(true)&&effect.paused&&call(character)==0))return -302;
 character[0x98]=1;if(!check(call(character)==0))return -303;character[0x98]=0;
 float value;memoryRead(effect.speedAddress,&value,4);if(!check(value==0&&effect.maintain()))return -304;
 if(!check(effect.setPause(false)&&call(character)==1))return -305;memoryRead(effect.speedAddress,&value,4);if(!check(value==1))return -306;
 if(!check(effect.setHud(true)&&effect.setPause(true)))return -307;effect.restore();memoryRead(effect.speedAddress,&value,4);if(!check(effect.flags()==0&&value==1&&call(character)==1))return -308;
 effect.pausePatch.removeTestHook();MH_Uninitialize();VirtualFree(allocation,0,MEM_RELEASE);return count;
}
extern "C" __declspec(dllexport) int DS3Camera_RunSlowTimeTests(){
 int count=0;auto check=[&](bool ok){if(ok)count++;return ok;};
 auto memory=(unsigned char*)VirtualAlloc(nullptr,8192,MEM_COMMIT|MEM_RESERVE,PAGE_EXECUTE_READWRITE);if(!memory)return -1;
 if(MH_Initialize()!=MH_OK)return -2;
 uintptr_t a=(uintptr_t)memory;Effects effect;effect.pauseSite=a+4;effect.speedAddress=a+0x1000;
 const unsigned char code[]={0x53,0x48,0x89,0xcb,0x80,0xbb,0x98,0,0,0,0,0x0f,0x94,0xc0,0x0f,0xb6,0xc0,0x5b,0xc3};memcpy(memory,code,sizeof(code));
 const unsigned char prefix[]={0xc7,0x83,0x60,0x09,0,0};memcpy((void*)(effect.speedAddress-6),prefix,6);float value=1;memoryWrite(effect.speedAddress,&value,4);FlushInstructionCache(GetCurrentProcess(),memory,sizeof(code));
 using Compare=int(*)(void*);Compare call=(Compare)memory;void* character=memory+0x200;
 if(!check(effect.setSlow(true)&&effect.timeScale()==.25f&&effect.flags()==4))return -10-count;
 if(!check(effect.setSlowRatio(.1f)&&effect.timeScale()==.1f))return -20-count;
 if(!check(effect.setPause(true)&&effect.timeScale()==0&&effect.flags()==6&&call(character)==0))return -30-count;
 if(!check(effect.setPause(false)&&effect.timeScale()==.1f&&effect.flags()==4&&call(character)==1))return -40-count;
 if(!check(effect.setPause(true)&&effect.setSlow(false)&&effect.timeScale()==0&&effect.flags()==2))return -50-count;
 if(!check(effect.setPause(false)&&effect.timeScale()==1&&effect.flags()==0))return -60-count;
 if(!check(effect.setPause(true)&&effect.setSlow(true)&&effect.setSlowRatio(.5f)&&effect.timeScale()==0))return -70-count;
 if(!check(effect.setPause(false)&&effect.timeScale()==.5f))return -80-count;
 effect.restore();if(!check(effect.flags()==0&&effect.timeScale()==1))return -90-count;
 for(int loop=0;loop<8;loop++){
  if(!check(effect.setSlow(true)&&effect.timeScale()==.5f))return -100-count;
  if(!check(effect.setPause(true)&&effect.setPause(false)&&effect.timeScale()==.5f))return -110-count;
  if(!check(effect.setSlow(false)&&effect.timeScale()==1&&!effect.speedOwned))return -120-count;
 }
 if(!check(!effect.setSlowRatio(0)&&!effect.setSlowRatio(NAN)&&!effect.setSlowRatio(1.1f)&&effect.timeScale()==1))return -130-count;
 value=.75f;memoryWrite(effect.speedAddress,&value,4);if(!check(!effect.setSlow(true)&&effect.timeScale()==.75f&&effect.flags()==0))return -140-count;
 value=1;memoryWrite(effect.speedAddress,&value,4);if(!check(effect.setSlow(true)))return -150-count;
 value=.8f;memoryWrite(effect.speedAddress,&value,4);if(!check(!effect.maintain()&&effect.timeScale()==.8f&&effect.flags()==0))return -160-count;
 value=1;memoryWrite(effect.speedAddress,&value,4);effect.setSlow(true);effect.setPause(true);effect.restore();if(!check(effect.timeScale()==1&&effect.flags()==0&&call(character)==1))return -170-count;
 effect.pausePatch.removeTestHook();MH_Uninitialize();VirtualFree(memory,0,MEM_RELEASE);return count;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){module=instance;DisableThreadLibraryCalls(instance);HANDLE t=CreateThread(nullptr,0,run,nullptr,0,nullptr);if(t)CloseHandle(t);}return TRUE;}
