#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <algorithm>

namespace camera {
constexpr uint32_t Magic=0x33434d44, Version=3;
enum Command : uint32_t {Toggle=1,Lock=2,RollReset=4,FovReset=8,Recenter=16,Disconnect=32,Attach=64,Portrait=128,Hud=256,Pause=512,Slow=1024,MoveByCamera=2048,MoveByGame=4096,MoveByCharacter=8192};
// Single-writer fields are copied while holding the named IPC mutex.
struct Packet {
 uint32_t magic=Magic,version=Version,clientPid=0;float timeScale=1;
 uint64_t heartbeat=0;
 uint32_t commands=0,focused=0;
 float moveX=0,moveY=0,moveZ=0,turnX=0,turnY=0,turnZ=0;
 float mouseX=0,mouseY=0,wheel=0;
 float speed=3,turnSpeed=1.3f,sensitivity=.002f,smoothing=.12f,fastMultiplier=5;
 uint32_t fast=0,state=0,ack=0,error=0;
 uint64_t frames=0;
 float x=0,y=0,z=0,fov=43;
 uint32_t notice=0;float zoom=0;
 uint32_t screenControls=1,keepFraming=1;int32_t outputRotation=0;uint32_t effectFlags=0,featureError=0;float aspect=16.f/9,renderFov=43;float slowMultiplier=.25f;
};
static_assert(sizeof(Packet)==168,"IPC packet layout changed");
struct Vec {
 double x=0,y=0,z=0;
 Vec operator+(Vec b)const{return{x+b.x,y+b.y,z+b.z};}
 Vec operator-(Vec b)const{return{x-b.x,y-b.y,z-b.z};}
 Vec operator*(double a)const{return{x*a,y*a,z*a};}
 double length()const{return std::sqrt(x*x+y*y+z*z);}
};
inline Vec cross(Vec a,Vec b){return{a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x};}
inline double dot(Vec a,Vec b){return a.x*b.x+a.y*b.y+a.z*b.z;}
struct Quat {
 double x=0,y=0,z=0,w=1;
 Quat unit()const{double n=std::sqrt(x*x+y*y+z*z+w*w);return n>1e-12?Quat{x/n,y/n,z/n,w/n}:Quat{};}
 Quat operator*(Quat b)const{return Quat{w*b.x+x*b.w+y*b.z-z*b.y,w*b.y-x*b.z+y*b.w+z*b.x,w*b.z+x*b.y-y*b.x+z*b.w,w*b.w-x*b.x-y*b.y-z*b.z}.unit();}
 Vec rotate(Vec v)const{Vec u{x,y,z};return v+cross(u,v)*(2*w)+cross(u,cross(u,v))*2;}
 static Quat axis(Vec a,double angle){double s=std::sin(angle/2);return{a.x*s,a.y*s,a.z*s,std::cos(angle/2)};}
 static Quat basis(Vec r,Vec u,Vec f){
  double tr=r.x+u.y+f.z,s;Quat q;
  if(tr>0){s=std::sqrt(tr+1)*2;q={ (u.z-f.y)/s,(f.x-r.z)/s,(r.y-u.x)/s,s/4};}
  else if(r.x>u.y&&r.x>f.z){s=std::sqrt(1+r.x-u.y-f.z)*2;q={s/4,(u.x+r.y)/s,(f.x+r.z)/s,(u.z-f.y)/s};}
  else if(u.y>f.z){s=std::sqrt(1+u.y-r.x-f.z)*2;q={(u.x+r.y)/s,s/4,(f.y+u.z)/s,(f.x-r.z)/s};}
  else{s=std::sqrt(1+f.z-r.x-u.y)*2;q={(f.x+r.z)/s,(f.y+u.z)/s,s/4,(r.y-u.x)/s};}
  return q.unit();
 }
};
struct alignas(16) Frame {unsigned char bytes[0x60]{};};
struct Pose {Vec position;Quat rotation;double fov=43;};
inline float getFloat(const Frame& f,int offset){float x;std::memcpy(&x,f.bytes+offset,4);return x;}
inline void putFloat(Frame& f,int offset,float x){std::memcpy(f.bytes+offset,&x,4);}
inline bool decode(const Frame& f,Pose& out){
 float a[16];std::memcpy(a,f.bytes+0x10,64);for(float x:a)if(!std::isfinite(x))return false;
 Vec r{a[0],a[1],a[2]},u{a[4],a[5],a[6]},v{a[8],a[9],a[10]};
 float fov=getFloat(f,0x50);if(!std::isfinite(fov)||fov<.08f||fov>2.63f||std::abs(r.length()-1)>.05||std::abs(u.length()-1)>.05||std::abs(v.length()-1)>.05||std::abs(dot(r,u))>.05||dot(cross(r,u),v)<.95)return false;
 out={{a[12],a[13],a[14]},Quat::basis(r,u,v),fov*180/3.141592653589793};return out.position.length()<10000000;
}
inline void encode(Frame& f,const Pose& p){
 Vec r=p.rotation.rotate({1,0,0}),u=p.rotation.rotate({0,1,0}),v=p.rotation.rotate({0,0,1});
 float a[16]={(float)r.x,(float)r.y,(float)r.z,0,(float)u.x,(float)u.y,(float)u.z,0,(float)v.x,(float)v.y,(float)v.z,0,(float)p.position.x,(float)p.position.y,(float)p.position.z,1};
 std::memcpy(f.bytes+0x10,a,64);putFloat(f,0x50,(float)(p.fov*3.141592653589793/180));
}
inline bool validConfig(const Packet& p){
 const float a[]={p.speed,p.turnSpeed,p.sensitivity,p.smoothing,p.fastMultiplier,p.moveX,p.moveY,p.moveZ,p.turnX,p.turnY,p.turnZ,p.mouseX,p.mouseY,p.wheel,p.zoom,p.slowMultiplier};
 for(float v:a)if(!std::isfinite(v))return false;
 return p.speed>=.01f&&p.speed<=100&&p.turnSpeed>=.01f&&p.turnSpeed<=10&&p.sensitivity>=.00001f&&p.sensitivity<=.05f&&p.smoothing>=0&&p.smoothing<=1&&p.fastMultiplier>=1&&p.fastMultiplier<=50&&std::abs(p.moveX)<=1&&std::abs(p.moveY)<=1&&std::abs(p.moveZ)<=1&&std::abs(p.turnX)<=1&&std::abs(p.turnY)<=1&&std::abs(p.turnZ)<=1&&std::abs(p.mouseX)<100000&&std::abs(p.mouseY)<100000&&std::abs(p.wheel)<1000&&std::abs(p.zoom)<=1&&p.screenControls==1&&p.keepFraming<=1&&p.slowMultiplier>=.05f&&p.slowMultiplier<=1;
}
inline Pose portraitPose(const Pose& logical,int degrees,double aspect,bool framing){
 Pose output=logical;output.rotation=logical.rotation*Quat::axis({0,0,1},degrees*3.141592653589793/180);
 if(degrees&&framing)output.fov=2*std::atan(std::tan(logical.fov*3.141592653589793/360)/aspect)*180/3.141592653589793;
 return output;
}
// Ground movement reference: the game reads a camera basis for character input, so
// publish yaw/pitch only. Manual bank and the F6 screen rotation stay out of it,
// otherwise a tilted or 90-degree picture would rotate W/D off the ground plane.
inline Pose followPose(const Pose& pose,double yaw,double pitch){
 Pose output=pose;output.rotation=Quat::axis({0,1,0},yaw)*Quat::axis({1,0,0},pitch);return output;
}
struct Motion {
 Pose pose;Vec velocity,angular;double initialFov=43,yaw=0,pitch=0,roll=0;bool on=false,locked=false,screenControls=true,keepFraming=true;int outputDegrees=0;double aspect=16./9;
 void clear(){velocity={};angular={};}
 void rebuildRotation(){pose.rotation=Quat::axis({0,1,0},yaw)*Quat::axis({1,0,0},pitch)*Quat::axis({0,0,1},roll);}
 void reset(const Pose& live){
  pose=live;initialFov=live.fov;locked=false;outputDegrees=0;clear();
  Vec f=live.rotation.rotate({0,0,1});yaw=std::atan2(f.x,f.z);pitch=std::asin(-std::clamp(f.y,-1.,1.));
  Quat base=Quat::axis({0,1,0},yaw)*Quat::axis({1,0,0},pitch);Vec right=live.rotation.rotate({1,0,0});
  roll=std::atan2(dot(right,base.rotate({0,1,0})),dot(right,base.rotate({1,0,0})));
 }
 void update(const Packet& p,double dt){
  if(!on||locked||!p.focused){clear();return;}
  dt=std::clamp(dt,0.,.05);Vec move{p.moveX,p.moveY,p.moveZ},turn{p.turnX,p.turnY,p.turnZ};
  if(move.length()>1)move=move*(1/move.length());double blend=p.smoothing<=0?1:1-std::exp(-dt/p.smoothing);
  velocity=velocity*(1-blend)+move*blend;angular=angular*(1-blend)+turn*(p.turnSpeed*blend);
  // Keep manual bank independent of the viewing direction. A local-axis quaternion
  // walk has geometric roll drift even if the roll key is never pressed.
  double horizontal=angular.y*dt+p.mouseX*p.sensitivity,vertical=angular.x*dt+p.mouseY*p.sensitivity;
  double controlRoll=roll+(screenControls?outputDegrees*3.141592653589793/180:0);
  double c=std::cos(controlRoll),s=std::sin(controlRoll);
  yaw=std::remainder(yaw+horizontal*c+vertical*s,2*3.141592653589793);
  pitch=std::clamp(pitch+vertical*c-horizontal*s,-89.0*3.141592653589793/180,89.0*3.141592653589793/180);
  roll=std::remainder(roll+angular.z*dt,2*3.141592653589793);
  rebuildRotation();
  Quat axes=screenControls?pose.rotation*Quat::axis({0,0,1},outputDegrees*3.141592653589793/180):pose.rotation;
  Vec right=axes.rotate({1,0,0}),forward=pose.rotation.rotate({0,0,1});
  double speed=p.speed*(p.fast?p.fastMultiplier:1);
  pose.position=pose.position+(right*velocity.x+Vec{0,velocity.y,0}+forward*velocity.z)*(dt*speed);
  // Acceleration is applied only to translation.
  pose.fov=std::clamp(pose.fov+p.zoom*25*dt-p.wheel*2.,5.,150.);
 }
 Pose rendered()const{return portraitPose(pose,outputDegrees,aspect,keepFraming);}
 void cyclePortrait(){outputDegrees=outputDegrees==0?90:outputDegrees==90?-90:0;clear();}
 void resetRoll(){roll=0;rebuildRotation();angular={};}
};
}
