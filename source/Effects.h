#pragma once
#include "CameraCore.h"
#include "vendor/minhook/include/MinHook.h"
#include <vector>
#include <tlhelp32.h>
namespace camera {
inline bool memoryRead(uintptr_t address,void* value,size_t size){SIZE_T n=0;return address>=0x10000&&ReadProcessMemory(GetCurrentProcess(),(void*)address,value,size,&n)&&n==size;}
inline bool memoryWrite(uintptr_t address,const void* value,size_t size){SIZE_T n=0;return address>=0x10000&&WriteProcessMemory(GetCurrentProcess(),(void*)address,value,size,&n)&&n==size;}
inline uintptr_t memoryPointer(uintptr_t address){uintptr_t value=0;memoryRead(address,&value,8);return value;}
struct ByteOverride {
 uintptr_t address=0;unsigned char original=0,owned=0;bool held=false;
 bool acquire(uintptr_t target,unsigned char value,bool hud){unsigned char before;if(!memoryRead(target,&before,1)||before>(hud?2:1))return false;
  if(!held){address=target;original=hud&&before==0?1:before;owned=value;}
  if(address!=target||!memoryWrite(target,&value,1))return false;held=true;return true;}
 bool maintain(){unsigned char value;return !held||(memoryRead(address,&value,1)&&(value==owned||memoryWrite(address,&owned,1)));}
 bool restore(bool sameOwner){if(!held)return true;unsigned char current;if(sameOwner){if(!memoryRead(address,&current,1))return false;if(current==owned&&!memoryWrite(address,&original,1))return false;}held=false;return true;}
};
// The pause comparison consumes only ZF at the following JE. A nonzero flag
// forces ZF=0 without changing any registers, then resumes after the original CMP.
class PausePatch {
 uintptr_t site=0;unsigned char* code=nullptr;void* trampoline=nullptr;bool enabled=false;
 static void jump(unsigned char* at,uintptr_t target){const unsigned char op[]={0xff,0x25,0,0,0,0};memcpy(at,op,6);memcpy(at+6,&target,8);}
public:
 bool prepare(uintptr_t target){
  if(code)return site==target;const unsigned char cmp[]={0x80,0xbb,0x98,0,0,0,0};unsigned char actual[7];
  if(!memoryRead(target,actual,7)||memcmp(actual,cmp,7))return false;
  auto allocation=(unsigned char*)VirtualAlloc(nullptr,8192,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!allocation)return false;
  DWORD previous;if(!VirtualProtect(allocation,4096,PAGE_EXECUTE_READWRITE,&previous)){VirtualFree(allocation,0,MEM_RELEASE);return false;}
  if(MH_CreateHook((void*)target,allocation,&trampoline)!=MH_OK){VirtualFree(allocation,0,MEM_RELEASE);return false;}
  allocation[0]=0x80;allocation[1]=0x3d;int32_t displacement=4096-7;memcpy(allocation+2,&displacement,4);allocation[6]=0;
  allocation[7]=0x75;allocation[8]=14;jump(allocation+9,(uintptr_t)trampoline);jump(allocation+23,target+7);
  FlushInstructionCache(GetCurrentProcess(),allocation,37);VirtualProtect(allocation,4096,PAGE_EXECUTE_READ,&previous);
  site=target;code=allocation;return true;
 }
 bool pause(uintptr_t target){if(!prepare(target))return false;if(!enabled){const unsigned char expected[]={0x80,0xbb,0x98,0,0,0,0};unsigned char current[7];if(!memoryRead(site,current,7)||memcmp(expected,current,7))return false;if(MH_EnableHook((void*)site)!=MH_OK)return false;enabled=true;}InterlockedExchange((LONG*)(code+4096),1);return true;}
 bool resume(){if(!code)return true;InterlockedExchange((LONG*)(code+4096),0);if(enabled){if(MH_DisableHook((void*)site)!=MH_OK)return false;enabled=false;}return true;}
 void removeTestHook(){resume();if(code)MH_RemoveHook((void*)site);/* Allocation remains executable until the process exits. */}
};
class FreezeThreads {
 std::vector<HANDLE> threads;size_t suspended=0;
public:
 bool freeze(){HANDLE snapshot=CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD,0);if(snapshot==INVALID_HANDLE_VALUE)return false;THREADENTRY32 entry{};entry.dwSize=sizeof(entry);
  if(Thread32First(snapshot,&entry))do{if(entry.th32OwnerProcessID==GetCurrentProcessId()&&entry.th32ThreadID!=GetCurrentThreadId()){HANDLE thread=OpenThread(THREAD_SUSPEND_RESUME,FALSE,entry.th32ThreadID);if(!thread){CloseHandle(snapshot);return false;}threads.push_back(thread);}}while(Thread32Next(snapshot,&entry));CloseHandle(snapshot);
  for(HANDLE thread:threads){if(SuspendThread(thread)==DWORD(-1))return false;suspended++;}return true;
 }
 ~FreezeThreads(){for(size_t i=0;i<suspended;i++)ResumeThread(threads[i]);for(HANDLE thread:threads)CloseHandle(thread);}
};
struct Effects {
 uintptr_t dataSlot=0,menuSlot=0,expectedMenuVtable=0,speedAddress=0,pauseSite=0;
 ByteOverride hud,menu;PausePatch pausePatch;bool hidden=false,paused=false,mock=false;float originalSpeed=1,ownedSpeed=1,slowRatio=.25f;bool speedOwned=false,slow=false;uint32_t error=0;
 void configure(uintptr_t base){dataSlot=base+0x47572b8;menuSlot=base+0x4796298;expectedMenuVtable=base+0x29797a0;speedAddress=base+0x9a3e78;pauseSite=base+0x8f306b;}
 uintptr_t hudAddress()const{return memoryPointer(memoryPointer(dataSlot)+0x58)+0x11;}
 uintptr_t menuAddress()const{uintptr_t owner=memoryPointer(menuSlot);return memoryPointer(owner)==expectedMenuVtable?owner+0x90:0;}
 bool setHud(bool hide){
  if(mock){hidden=hide;error=0;return true;}
  uintptr_t h=hudAddress(),m=menuAddress();unsigned char hv,mv;
  if(hide){
   if(!memoryRead(h,&hv,1)||hv>2||!memoryRead(m,&mv,1)||mv>1){error=11;return false;}
   if(!hud.acquire(h,0,true)||!menu.acquire(m,1,false)){hud.restore(hudAddress()==hud.address);menu.restore(menuAddress()==menu.address);hidden=hud.held||menu.held;error=11;return false;}
   hidden=true;
  }else{bool ok=hud.restore(h==hud.address);ok=menu.restore(m==menu.address)&&ok;hidden=hud.held||menu.held;if(!ok){error=11;return false;}}
  error=0;return true;
 }
 bool scalar(float value){
  FreezeThreads freeze;if(!freeze.freeze())return false;
  DWORD old;if(!VirtualProtect((void*)speedAddress,4,PAGE_EXECUTE_READWRITE,&old))return false;
  bool ok=memoryWrite(speedAddress,&value,4);FlushInstructionCache(GetCurrentProcess(),(void*)speedAddress,4);DWORD unused;VirtualProtect((void*)speedAddress,4,old,&unused);return ok;
 }
 bool ownTime(){
  if(mock)return true;
  float current;if(!memoryRead(speedAddress,&current,4)||!std::isfinite(current))return false;
  if(speedOwned)return current==ownedSpeed;
  const unsigned char prefix[]={0xc7,0x83,0x60,0x09,0,0};unsigned char bytes[6];
  if(!memoryRead(speedAddress-6,bytes,6)||memcmp(bytes,prefix,6)||std::abs(current-1)>.001f)return false;
  originalSpeed=ownedSpeed=current;speedOwned=true;return true;
 }
 bool applyTime(){
  if(mock)return true;
  if(!speedOwned&&!paused&&!slow)return true;
  if(!ownTime())return false;
  float target=paused?0:slow?slowRatio:originalSpeed;
  if(target!=ownedSpeed){if(!scalar(target))return false;ownedSpeed=target;}
  if(!paused&&!slow)speedOwned=false;
  return true;
 }
 bool setSlowRatio(float ratio){
  if(!std::isfinite(ratio)||ratio<.05f||ratio>1){error=13;return false;}
  if(ratio==slowRatio)return true;float previous=slowRatio;slowRatio=ratio;
  if(slow&&!applyTime()){slowRatio=previous;error=13;return false;}return true;
 }
 bool setSlow(bool enabled){
  if(enabled==slow)return true;bool previous=slow;slow=enabled;
  if(!applyTime()){slow=previous;error=13;return false;}error=0;return true;
 }
 bool setPause(bool freeze){
  if(mock){paused=freeze;error=0;return true;}
  if(freeze){
   if(paused)return true;
   if(!ownTime()){error=12;return false;}
   if(!pausePatch.pause(pauseSite)){applyTime();error=12;return false;}
   paused=true;
   if(!applyTime()){paused=false;pausePatch.resume();applyTime();error=12;return false;}
  }else{
   bool ok=pausePatch.resume();paused=false;
   // Another mod owns a changed scalar: release our override without overwriting it.
   if(speedOwned){float current;if(memoryRead(speedAddress,&current,4)&&current!=ownedSpeed){speedOwned=false;slow=false;}}
   if(!applyTime()||!ok){error=12;return false;}
  }
  error=0;return true;
 }
 bool maintain(){
  if(mock)return true;
  if(hidden&&(hudAddress()!=hud.address||menuAddress()!=menu.address||!hud.maintain()||!menu.maintain())){setHud(false);error=11;return false;}
  if(speedOwned){float speed;if(!memoryRead(speedAddress,&speed,4)||speed!=ownedSpeed){pausePatch.resume();paused=slow=speedOwned=false;error=13;return false;}}
  return true;
 }
 void restore(){slow=false;setPause(false);uint32_t previous=error;setHud(false);if(previous)error=previous;}
 float timeScale()const{float value;if(mock)return paused?0:slow?slowRatio:1;if(memoryRead(speedAddress,&value,4)&&std::isfinite(value)&&value>=0&&value<=10)return value;return 1;}
 uint32_t flags()const{return(hidden?1u:0u)|(paused?2u:0u)|(slow?4u:0u)|((speedOwned&&!paused&&!slow)?8u:0u);}
};
}
