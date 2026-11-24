using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {

public class PreviewForm:Form {
 static readonly Color Bg=Color.FromArgb(5,5,5), Side=Color.FromArgb(17,17,17), Surface=Color.FromArgb(37,37,37), Edge=Color.FromArgb(47,47,47), Ink=Color.FromArgb(238,238,238), Muted=Color.FromArgb(168,168,168), Accent=Color.FromArgb(183,107,66);
 readonly float scale;
 protected Settings settings;
 protected Binding[] pendingBindings;
 Binding[] ViewBindings{get{return pendingBindings??settings.Bindings;}}
 readonly UiMotion motion=new UiMotion();readonly System.Windows.Forms.Timer animationTimer=new System.Windows.Forms.Timer();
 bool staticRender,keyboardFocus;
 public const string RepositoryUrl="https://github.com/Sylva-06/DarkSoulsIIICamera";
 readonly ToolTip repositoryTip=new ToolTip{InitialDelay=400,ReshowDelay=100,AutoPopDelay=3000};
 protected string loadError="";
 protected string processText="尚未检测到游戏进程",message="未连接";
 protected bool gameAvailable,connected,injectionBusy;
 readonly List<Hit> hits=new List<Hit>();
 readonly List<RectangleF> textBoxes=new List<RectangleF>();
 readonly Image mark;
 readonly Image gameWordmark,gameBonfire;
 readonly KeyboardMap map=new KeyboardMap();
 Bitmap bodySurface,signatureSurface,markSurface,keyboardSurface;
 readonly UiTextCache textCache=new UiTextCache();
 RectangleF hoveredBounds;
 readonly string[] pages={"连接与注入","按键设置","速度与手感","UI、暂停与竖屏","操作说明","注意事项"};
 readonly string[] metric={"相机移动速度","旋转速度","鼠标灵敏度","平滑响应","按住加速倍率","战斗减速倍率"};
 readonly string[] units={"单位 / 秒","弧度 / 秒","","秒","倍","倍"};
 protected readonly double[] values=new double[6];
 static readonly double[] lower={.01,.01,.00001,0,1,.05},upper={100,10,.05,1,50,1},step={.01,.01,.0001,.01,1,.01};
 protected int page,selectedKey,orientation,source;int tab,keyOffset;
 protected bool following,framing,hidden,paused,slowed;bool bodyContext;
 PointF pointer=new PointF(-1,-1);int focusIndex=-1;

 [DllImport("user32.dll")]static extern bool ReleaseCapture();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 sealed class Hit {public RectangleF Bounds;public Action Action;public bool Hand;public string Name;}
 float W{get{return ClientSize.Width/scale;}}float H{get{return ClientSize.Height/scale;}}
 // Keep a reading-width canvas centered in the area to the right of navigation.
 float CW{get{return Math.Min(960,W-224-96);}}
 float X{get{return 224+(W-224-CW)/2;}}
 float contentScroll;
 float ViewH{get{return Math.Max(664,H);}}
 float BaseOffset{get{return Math.Min(80,Math.Max(0,(H-800)*.25f));}}
 float BodyOffset{get{return BaseOffset-contentScroll;}}
 RectangleF BodyViewport{get{return new RectangleF(X-8,72,CW+16,Math.Max(0,H-108));}}
 float ValueX{get{return X+Math.Min(264,CW*.36f);}}
 int D(float v){return (int)Math.Round(v*scale);}
 public PreviewForm():this(PreviewDpi.Scale){}
 internal PreviewForm(float factor){
  scale=factor;
  Text="Dark Souls III Camera";FormBorderStyle=FormBorderStyle.None;AutoScaleMode=AutoScaleMode.None;
  var work=Screen.FromPoint(Cursor.Position).WorkingArea;
  MinimumSize=new Size(Math.Min(D(840),work.Width),Math.Min(D(520),work.Height));ClientSize=new Size(Math.Min(D(1200),work.Width),Math.Min(D(800),work.Height));StartPosition=FormStartPosition.CenterScreen;
  BackColor=Bg;Font=new Font("Microsoft YaHei UI",12);DoubleBuffered=true;KeyPreview=true;SetStyle(ControlStyles.ResizeRedraw,true);
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  mark=LoadImage("camera-icon.png");gameWordmark=LoadImage("game-wordmark.png");gameBonfire=LoadImage("bonfire-symbol.png");
  try{settings=Settings.Load(Path.Combine(Log.Root,"camera-settings.xml"));}catch(Exception e){loadError="配置读取失败，原文件保留："+e.Message;settings=new Settings();}
  map.Visible=false;
  animationTimer.Interval=15;animationTimer.Tick+=(s,e)=>{Invalidate();if(!motion.Active)animationTimer.Stop();};
  MouseMove+=(s,e)=>{pointer=new PointF(e.X/scale,e.Y/scale);var hit=hits.LastOrDefault(h=>h.Bounds.Contains(pointer));var bounds=hit==null?RectangleF.Empty:hit.Bounds;Cursor=hit!=null&&hit.Hand?Cursors.Hand:Cursors.Default;if(bounds!=hoveredBounds){hoveredBounds=bounds;repositoryTip.SetToolTip(this,hit!=null&&hit.Name=="项目仓库"?"打开 GitHub 仓库":null);Invalidate();}};
  MouseLeave+=(s,e)=>{repositoryTip.SetToolTip(this,null);pointer=new PointF(-1,-1);if(hoveredBounds!=RectangleF.Empty){hoveredBounds=RectangleF.Empty;Invalidate();}};
  MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;float x=e.X/scale,y=e.Y/scale;var hit=hits.LastOrDefault(h=>h.Bounds.Contains(x,y));if(hit!=null){focusIndex=hits.IndexOf(hit);keyboardFocus=false;motion.Start("press:"+hit.Name,1,0,160);animationTimer.Start();RunAction(hit.Action);if(e.Clicks==2&&hit.Name!=null&&hit.Name.StartsWith("按键行:"))RunAction(OnCaptureKey);Invalidate();}else if(y<56){if(e.Clicks==2)ToggleMaximize();else{ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}};
  MouseWheel+=(s,e)=>{if(e.X>D(224)){if(page==1)keyOffset=Math.Max(0,Math.Min(ViewBindings.Length-VisibleRows(),keyOffset+(e.Delta<0?3:-3)));else contentScroll=Math.Max(0,Math.Min(ViewH-H,contentScroll+(e.Delta<0?64:-64)));Invalidate();}};
  Resize+=(s,e)=>{contentScroll=Math.Min(contentScroll,ViewH-H);focusIndex=-1;Invalidate();};
  ShowPage(0);
 }
 static Image LoadImage(string name){using(var stream=typeof(PreviewForm).Assembly.GetManifestResourceStream("DS3Camera."+name)){if(stream==null)throw new FileNotFoundException("缺少界面资源",name);using(var image=Image.FromStream(stream))return new Bitmap(image);}}
 void RunAction(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
 protected virtual void OnInject(){} protected virtual void OnDisconnect(){} protected virtual void OnCommand(uint command){} protected virtual void OnRefresh(){} protected virtual void OnSave(){} protected virtual void OnCaptureKey(){} protected virtual void OnDirection(int index){}
 protected virtual void OpenRepository(string url){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}
 protected void SetStatus(string text){if(message==text)return;message=text;Invalidate();}
 protected void RefreshKeys(){Invalidate();}
 protected void ReadSettingsPage(){
  if(page==2){settings.MovementSpeed=values[0];settings.RotationSpeed=values[1];settings.MouseSensitivity=values[2];settings.Smoothing=values[3];settings.FastMultiplier=values[4];settings.SlowMultiplier=values[5];}
  if(page==3){settings.PortraitKeepVerticalFraming=framing;settings.FollowCameraDirection=following;settings.FollowCharacterFacing=source==1;}
 }
 public void ShowPage(int index){
  if(page!=index&&!staticRender){motion.Start("body",0,1,150);animationTimer.Start();}
  page=index;contentScroll=0;focusIndex=-1;
  if(index==0)OnRefresh();
  if(index==2){double[] data={settings.MovementSpeed,settings.RotationSpeed,settings.MouseSensitivity,settings.Smoothing,settings.FastMultiplier,settings.SlowMultiplier};Array.Copy(data,values,6);}
  if(index==3){framing=settings.PortraitKeepVerticalFraming;following=settings.FollowCameraDirection;source=settings.FollowCharacterFacing?1:0;}
  Invalidate();
 }
 protected void UpdateRuntimeView(bool active,int rotation,uint effects){
  bool changed=connected!=active||orientation!=rotation||hidden!=((effects&1)!=0)||paused!=((effects&2)!=0)||slowed!=((effects&4)!=0);
  connected=active;orientation=rotation;hidden=(effects&1)!=0;paused=(effects&2)!=0;slowed=(effects&4)!=0;if(changed)Invalidate();
 }
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
  Keys key=keyData&Keys.KeyCode;
  if(key==System.Windows.Forms.Keys.Tab){keyboardFocus=true;focusIndex=(focusIndex+((keyData&System.Windows.Forms.Keys.Shift)!=0?-1:1)+hits.Count)%Math.Max(1,hits.Count);Invalidate();return true;}
  if(key==System.Windows.Forms.Keys.Enter&&focusIndex>=0&&focusIndex<hits.Count){motion.Start("press:"+hits[focusIndex].Name,1,0,160);animationTimer.Start();RunAction(hits[focusIndex].Action);Invalidate();return true;}
  if(page!=1&&H<664&&(key==System.Windows.Forms.Keys.PageDown||key==System.Windows.Forms.Keys.PageUp)){contentScroll=Math.Max(0,Math.Min(ViewH-H,contentScroll+(key==System.Windows.Forms.Keys.PageDown?120:-120)));Invalidate();return true;}
  if(page==1&&(key==System.Windows.Forms.Keys.Up||key==System.Windows.Forms.Keys.Down||key==System.Windows.Forms.Keys.PageUp||key==System.Windows.Forms.Keys.PageDown)){
   selectedKey=Math.Max(0,Math.Min(ViewBindings.Length-1,selectedKey+(key==System.Windows.Forms.Keys.Up?-1:key==System.Windows.Forms.Keys.Down?1:key==System.Windows.Forms.Keys.PageUp?-VisibleRows():VisibleRows())));
   keyOffset=Math.Max(0,Math.Min(selectedKey,Math.Max(keyOffset,selectedKey-VisibleRows()+1)));Invalidate();return true;
  }
  return base.ProcessCmdKey(ref msg,keyData);
 }
 void ToggleMaximize(){MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
 RectangleF ScreenRect(RectangleF rect){if(bodyContext)rect.Y+=BodyOffset;return rect;}
 bool Hover(RectangleF rect){return ScreenRect(rect).Contains(pointer);}
 void Rect(Graphics g,Color c,float x,float y,float w,float h){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
 void Line(Graphics g,float x,float y,float xx,float yy,Color? c=null){using(var p=new Pen(c??Edge,1/scale))g.DrawLine(p,x,y,xx,yy);}
 void TextAt(Graphics g,string text,float x,float y,float width,float height,float size,Color? color=null,bool bold=false,TextFormatFlags extra=0){
  float[] matrix;using(var transform=g.Transform)matrix=transform.Elements;
  var mapped=new Rectangle((int)Math.Round(x*matrix[0]+matrix[4]),(int)Math.Round(y*matrix[3]+matrix[5]),(int)Math.Round(width*matrix[0]),(int)Math.Round(height*matrix[3]));
  var saved=g.Save();g.ResetTransform();
  textCache.Draw(g,text,mapped,D(size),bold,color??Ink,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|extra);
  g.Restore(saved);
  var textRect=ScreenRect(new RectangleF(x,y,width,height));textBoxes.Add(bodyContext?RectangleF.Intersect(textRect,BodyViewport):textRect);
 }
 void HitAt(RectangleF r,Action a,bool hand=true,string name=null){r=ScreenRect(r);if(bodyContext)r=RectangleF.Intersect(r,BodyViewport);if(r.Width>0&&r.Height>0)hits.Add(new Hit{Bounds=r,Action=a,Hand=hand,Name=name});}
 protected void ClickView(string name){Refresh();var hit=hits.Last(h=>h.Name==name);OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,D(hit.Bounds.X+hit.Bounds.Width/2),D(hit.Bounds.Y+hit.Bounds.Height/2),0));Refresh();}
 float TextWidth(string text,float size){using(var f=new Font("Microsoft YaHei UI",D(size),FontStyle.Regular,GraphicsUnit.Pixel))return TextRenderer.MeasureText(text,f,Size.Empty,TextFormatFlags.NoPadding).Width/scale;}
 void Button(Graphics g,string text,float x,float y,float w,Action a,bool primary=false,bool active=false,bool enabled=true){
  var r=new RectangleF(x,y,w,44);float hover=Anim("hover:button:"+text,enabled&&Hover(r)?1:0,110),press=Anim("press:"+text,0,160);
  Color normal=!enabled?Color.FromArgb(21,21,21):primary?Color.FromArgb(55,55,55):active?Color.FromArgb(43,36,31):Surface;
  Color bg=Blend(normal,Color.FromArgb(primary?72:50,primary?72:50,primary?72:50),hover);bg=Blend(bg,Color.FromArgb(77,65,55),enabled?press*.65f:0);
  Rect(g,bg,x,y,w,44);if(enabled&&(primary||active||press>.01f)){using(var p=new Pen(active?Accent:Blend(Color.FromArgb(83,83,83),Accent,press),1/scale))g.DrawRectangle(p,x+.5f,y+.5f,w-1,43);}
  TextAt(g,text,x+12,y,w-24,44,14,enabled?Ink:Color.FromArgb(101,101,101),false,TextFormatFlags.HorizontalCenter);
  if(enabled)HitAt(r,a,name:text);
 }
 void Label(Graphics g,string text,float y){TextAt(g,text,X,y,CW,26,14,Muted);}
 void Section(Graphics g,string text,float y){TextAt(g,text,X,y,CW,30,16,Ink);}
 void Body(Graphics g,string text,float y,float h=52){TextAt(g,text,X,y,CW,h,14,Muted,false,TextFormatFlags.WordBreak|TextFormatFlags.Top);}

 void Toggle(Graphics g,string text,float y,bool value,Action action){
  var rect=new RectangleF(X,y,CW,42);float hover=Anim("hover:toggle:"+text,Hover(rect)?1:0,110);if(hover>0)Rect(g,Blend(Bg,Color.FromArgb(18,18,18),hover),X,y,CW,42);
  TextAt(g,text,X+8,y,CW-86,42,15);
  float x=X+CW-52,on=Anim("toggle:"+text,value?1:0,180);Rect(g,Blend(Edge,Accent,on),x,y+10,44,22);Rect(g,Ink,x+3+22*on,y+13,16,16);
  HitAt(rect,action,name:text);Line(g,X,y+54,X+CW,y+54);
 }
 float Anim(string id,float target,int duration=180){return staticRender?target:motion.Value(id,target,duration);}
 static Color Blend(Color a,Color b,float t){t=Math.Max(0,Math.Min(1,t));return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
 void Segments(Graphics g,string id,string[] labels,float y,int selected,bool enabled,Action<int> select){
  float width=CW/labels.Length;Rect(g,Color.FromArgb(23,23,23),X,y,CW,44);
  float position=Anim("segment:"+id,selected,210);Rect(g,enabled?Color.FromArgb(49,49,49):Color.FromArgb(29,29,29),X+position*width+3,y+3,width-6,38);
  Line(g,X+position*width+16,y+43,X+(position+1)*width-16,y+43,enabled?Accent:Edge);
  for(int i=0;i<labels.Length;i++){int index=i;var rect=new RectangleF(X+i*width,y,width,44);float hover=Anim("hover:"+id+i,enabled&&Hover(rect)?1:0,110);
   if(enabled&&i!=selected&&hover>0)Rect(g,Blend(Color.FromArgb(23,23,23),Color.FromArgb(36,36,36),hover),rect.X+3,y+3,width-6,38);
   TextAt(g,labels[i],rect.X+8,y,width-16,44,14,!enabled?Color.FromArgb(100,100,100):Blend(Muted,Ink,Anim("selected:"+id+i,i==selected?1:0)),false,TextFormatFlags.HorizontalCenter);
   if(enabled)HitAt(rect,()=>select(index),name:labels[i]);
  }
 }
 protected void RestoreDefaultBindings(){pendingBindings=new Settings().Bindings;selectedKey=0;keyOffset=0;SetStatus("已恢复默认按键预览，保存后生效");RefreshKeys();}
 protected override void WndProc(ref Message m){
  base.WndProc(ref m);
  if(m.Msg!=0x84||WindowState!=FormWindowState.Normal||m.Result!=new IntPtr(1))return;
  long value=m.LParam.ToInt64();Point point=PointToClient(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));
  int border=D(6);bool left=point.X<border,right=point.X>=ClientSize.Width-border,top=point.Y<border,bottom=point.Y>=ClientSize.Height-border;
  int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
  m.Result=new IntPtr(hit);
 }
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.Clear(Bg);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(scale,scale);hits.Clear();textBoxes.Clear();bodyContext=false;
  Rect(g,Side,0,0,192,H-32);
  using(var fade=new LinearGradientBrush(new RectangleF(192,0,56,H-32),Side,Bg,LinearGradientMode.Horizontal))g.FillRectangle(fade,192,0,56,H-32);
  Rect(g,Color.FromArgb(18,18,18),0,H-32,W,32);
  Line(g,0,H-32,W,H-32,Color.FromArgb(28,28,28));
  if(markSurface==null){markSurface=new Bitmap(D(32),D(32),PixelFormat.Format32bppPArgb);using(var icon=Graphics.FromImage(markSurface)){icon.InterpolationMode=InterpolationMode.HighQualityBicubic;icon.DrawImage(mark,new Rectangle(Point.Empty,markSurface.Size));}}
  var iconState=g.Save();g.ResetTransform();g.DrawImageUnscaled(markSurface,D(28),D(12));g.Restore(iconState);
  float navPosition=Anim("nav-position",page,180);Rect(g,Accent,16,100+navPosition*56,2,20);
  for(int i=0;i<pages.Length;i++){int n=i;float y=88+i*56;float hover=Anim("hover:nav:"+i,Hover(new RectangleF(16,y,192,44))?1:0,110);if(hover>0)Rect(g,Blend(Side,Color.FromArgb(26,26,26),hover),24,y+2,176,40);
   TextAt(g,pages[i],32,y,176,44,14,Blend(Muted,Ink,Anim("nav-ink:"+i,page==i?1:0,180)));HitAt(new RectangleF(16,y,192,44),()=>ShowPage(n),name:"导航:"+pages[i]);}
  DrawGameSignature(g);
  const string product="Dark Souls III Camera";
  var titleBounds=new RectangleF(72,8,248,40);
  TextAt(g,product,76,14,240,28,14,Blend(Ink,Color.White,Anim("hover:repository",Hover(titleBounds)?1:0,110)));
  HitAt(titleBounds,()=>OpenRepository(RepositoryUrl),name:"项目仓库");

  TopButton(g,"—",W-138,()=>WindowState=FormWindowState.Minimized);
  TopButton(g,"□",W-92,ToggleMaximize);
  TopButton(g,"×",W-46,Close);
  TextAt(g,message,X,H-31,W-X-24,30,11,Muted,false,TextFormatFlags.EndEllipsis);

  map.Visible=false;
  // A bounded content surface keeps native GDI text inside the scrolling viewport.
  int bw=D(BodyViewport.Width),bh=D(BodyViewport.Height);
  if(bodySurface==null||bodySurface.Width!=bw||bodySurface.Height!=bh){if(bodySurface!=null)bodySurface.Dispose();bodySurface=new Bitmap(bw,bh,PixelFormat.Format32bppPArgb);}
  bodyContext=true;
  using(var body=Graphics.FromImage(bodySurface)){
   body.Clear(Bg);body.SmoothingMode=SmoothingMode.AntiAlias;body.ScaleTransform(scale,scale);body.TranslateTransform(-BodyViewport.X,-BodyViewport.Y+BodyOffset);
   TextAt(body,pages[page],X,88,CW,44,24,Ink);
   if(page==0)Connection(body);else if(page==1)KeysPage(body);else if(page==2)Speeds(body);else if(page==3)Scene(body);else if(page==4)Help(body);else Notes(body);
  }
  bodyContext=false;var surfaceState=g.Save();g.ResetTransform();g.DrawImageUnscaled(bodySurface,D(BodyViewport.X),D(BodyViewport.Y));g.Restore(surfaceState);
  if(H<664&&page!=1){float trackHeight=H-144,thumbHeight=trackHeight*H/ViewH,thumbY=86+(trackHeight-thumbHeight)*contentScroll/(ViewH-H);
   Rect(g,Edge,X+CW+8,86,2,trackHeight);Rect(g,Muted,X+CW+7,thumbY,4,thumbHeight);
   HitAt(new RectangleF(X+CW+2,86,16,trackHeight),()=>{contentScroll=contentScroll>=ViewH-H?0:Math.Min(ViewH-H,contentScroll+120);},name:"滚动内容");
  }
  float reveal=Anim("body",1,150);if(reveal<.999f)using(var veil=new SolidBrush(Color.FromArgb((int)((1-reveal)*255),Bg)))g.FillRectangle(veil,X-8,78,CW+16,H-112);
  if(keyboardFocus&&focusIndex>=0&&focusIndex<hits.Count){var r=hits[focusIndex].Bounds;r.Inflate(3,3);using(var focusPen=new Pen(Accent,1/scale)){focusPen.DashStyle=DashStyle.Dot;g.DrawRectangle(focusPen,r.X,r.Y,r.Width,r.Height);}}
  using(var pen=new Pen(Edge,1/scale))g.DrawRectangle(pen,0,0,W-1/scale,H-1/scale);
  if(!staticRender&&motion.Active&&!animationTimer.Enabled)animationTimer.Start();
 }
 void TopButton(Graphics g,string label,float x,Action action){if(Hover(new RectangleF(x,0,46,56)))Rect(g,label=="×"?Color.FromArgb(100,42,33):Color.FromArgb(30,30,30),x,0,46,56);TextAt(g,label,x,0,46,56,17,Muted,false,TextFormatFlags.HorizontalCenter);HitAt(new RectangleF(x,0,46,56),action);}
 void Connection(Graphics g){
  Label(g,"游戏进程",160);
  float by=200;Rect(g,Surface,X,by,CW,80);
  using(var brush=new SolidBrush(connected?Accent:Muted))g.FillEllipse(brush,X+20,by+23,6,6);
  TextAt(g,processText,X+38,by+11,CW-200,30,17,null,false,TextFormatFlags.EndEllipsis);
  TextAt(g,connected?"已连接":gameAvailable?"可连接":"等待游戏启动",X+38,by+43,CW-190,24,12,Muted);
  Button(g,"刷新",X+CW-112,by+22,88,OnRefresh,false,false,!injectionBusy);
  TextAt(g,"相机组件",X,320,ValueX-X-24,44,14,Muted);TextAt(g,"DS3CameraCore.dll",ValueX,320,X+CW-ValueX,44,15);Line(g,X,388,X+CW,388);
  Button(g,"注入相机",X,416,160,OnInject,true,false,gameAvailable&&!connected&&!injectionBusy);
  Button(g,"恢复并断开",X+176,416,160,OnDisconnect,false,false,connected&&!injectionBusy);
 }
 int VisibleRows(){return Math.Min(12,Math.Max(3,(int)((H-BaseOffset-332)/41)));}
 string ActionName(string action){switch(action){case "回到人物附近":return "复位到人物附近";case "隐藏或显示UI":return "隐藏 / 显示 UI";case "暂停或继续":return "暂停 / 继续";case "减缓时间或恢复":return "减速 / 正常";case "缩小FOV":return "拉近画面";case "增大FOV":return "拉远画面";default:return action;}}
 void KeysPage(Graphics g){
  float y=164,keyX=X+CW*.62f;int count=VisibleRows();keyOffset=Math.Max(0,Math.Min(ViewBindings.Length-count,keyOffset));
  TextAt(g,"操作",X+14,y,CW*.60f,34,12,Muted);TextAt(g,"绑定按键",keyX,y,CW*.30f,34,12,Muted);Line(g,X,y+39,X+CW,y+39);
  for(int k=0;k<count;k++){int index=k+keyOffset;float yy=y+40+k*41;var rect=new RectangleF(X,yy,CW-14,41);string id="按键行:"+index;
   float chosen=Anim("key-selected:"+index,index==selectedKey?1:0,180),hover=Anim("key-hover:"+index,Hover(rect)?1:0,110),press=Anim("press:"+id,0,160);
   Color color=Blend(Bg,Color.FromArgb(22,22,22),hover);color=Blend(color,Surface,chosen);color=Blend(color,Color.FromArgb(62,49,39),press*.5f);Rect(g,color,X,yy,CW-14,41);
   if(chosen>.01f)Rect(g,Blend(Bg,Accent,chosen),X,yy+10,2,21);
   TextAt(g,ActionName(ViewBindings[index].Action),X+14,yy,CW*.6f-22,41,14);
   TextAt(g,Settings.Name(ViewBindings[index].Key),keyX,yy,CW*.30f,41,14,Blend(Ink,Color.FromArgb(211,152,116),chosen));
   Line(g,X+14,yy+41,X+CW-24,yy+41);HitAt(rect,()=>{selectedKey=index;},name:id);}
  float track=y+44,trackH=count*41-8,thumb=Math.Max(38,trackH*count/ViewBindings.Length);
  Rect(g,Edge,X+CW-4,track,2,trackH);Rect(g,Muted,X+CW-5,track+(trackH-thumb)*keyOffset/Math.Max(1,ViewBindings.Length-count),4,thumb);
  HitAt(new RectangleF(X+CW-16,track,22,trackH),()=>{keyOffset=Math.Min(ViewBindings.Length-count,keyOffset+count);});
  float bottom=y+40+count*41+24;Button(g,"修改选中按键",X,bottom,156,OnCaptureKey);Button(g,"保存并应用",X+172,bottom,156,OnSave,true);Button(g,"恢复默认按键",X+344,bottom,156,RestoreDefaultBindings);
  if(pendingBindings!=null)TextAt(g,"按键修改待保存",X,bottom+52,CW,22,12,Color.FromArgb(211,152,116));
 }
 void Speeds(Graphics g){
  float valueX=ValueX,valueW=X+CW-valueX;float y=160;
  for(int i=0;i<6;i++){int n=i;float yy=y+i*64;TextAt(g,metric[i],X,yy,valueX-X-24,44,15);Rect(g,Surface,valueX,yy,valueW,44);
   string value=values[i].ToString(i==2?"0.00000":i==4?"0.0":"0.00",System.Globalization.CultureInfo.InvariantCulture);
   if(Hover(new RectangleF(valueX,yy,valueW,44)))Rect(g,Color.FromArgb(48,48,48),valueX,yy,valueW,44);
   TextAt(g,value,valueX+16,yy,valueW-116,44,16);TextAt(g,units[i],valueX+valueW-151,yy,104,44,11,Muted,false,TextFormatFlags.Right);
   Line(g,valueX+valueW-36,yy,valueX+valueW-36,yy+44);
   TextAt(g,"+",valueX+valueW-35,yy,34,22,14,Muted,false,TextFormatFlags.HorizontalCenter);
   TextAt(g,"−",valueX+valueW-35,yy+22,34,22,14,Muted,false,TextFormatFlags.HorizontalCenter);
   HitAt(new RectangleF(valueX,yy,valueW-36,44),()=>EditNumber(n));
   HitAt(new RectangleF(valueX+valueW-36,yy,36,22),()=>AdjustNumber(n,1));
   HitAt(new RectangleF(valueX+valueW-36,yy+22,36,22),()=>AdjustNumber(n,-1));
  }
  Button(g,"保存并应用",X,y+6*64+32,160,OnSave,true);
 }
 void AdjustNumber(int n,int sign){values[n]=Math.Max(lower[n],Math.Min(upper[n],Math.Round(values[n]+sign*step[n],5)));}
 void EditNumber(int n){using(var dialog=new NumberDialog(metric[n],values[n],lower[n],upper[n],step[n],n==2?5:n==4?1:2)){if(dialog.ShowDialog(this)==DialogResult.OK)values[n]=dialog.Value;}}
 void Scene(Graphics g){
  Label(g,"画面与游戏状态",164);float third=(CW-24)/3;
  Button(g,"隐藏 / 显示 UI",X,204,third,()=>OnCommand(256),false,hidden,connected);
  Button(g,"暂停 / 继续",X+third+12,204,third,()=>OnCommand(512),false,paused,connected);
  Button(g,"减速 / 正常",X+2*(third+12),204,third,()=>OnCommand(1024),false,slowed,connected);
  Label(g,"画面方向",280);Segments(g,"orientation",new[]{"横屏 0°","顺时针 90°","逆时针 90°"},316,orientation,connected,OnDirection);
  Toggle(g,"竖屏保持上下构图",379,framing,()=>framing=!framing);
  Toggle(g,"人物移动方向参考",443,following,()=>following=!following);
  Segments(g,"source",new[]{"按画面水平方向","按人物自身朝向"},510,source,following,index=>source=index);
  Button(g,"保存并应用",X,576,160,OnSave,true);
 }
 void Help(Graphics g){
  float y=152;Rect(g,Accent,X+Anim("help-tab",tab,180)*126,y+40,65,2);
  for(int i=0;i<2;i++){int n=i;float xx=X+i*126;TextAt(g,i==0?"默认按键":"使用流程",xx,y,108,42,14,Blend(Muted,Ink,Anim("help-ink:"+i,tab==i?1:0)));HitAt(new RectangleF(xx,y,108,42),()=>{tab=n;},name:i==0?"默认按键":"使用流程");}
  if(tab==0){float width=Math.Min(CW,(ViewH-BaseOffset-262)*829/552);float hh=width*552/829;map.SetBounds(D(X+(CW-width)/2),D(220+BodyOffset),D(width),D(hh));
   if(keyboardSurface==null||keyboardSurface.Width!=D(width)||keyboardSurface.Height!=D(hh)){if(keyboardSurface!=null)keyboardSurface.Dispose();keyboardSurface=new Bitmap(D(width),D(hh),PixelFormat.Format32bppPArgb);using(var diagram=Graphics.FromImage(keyboardSurface))KeyboardMap.Draw(diagram,keyboardSurface.Width,keyboardSurface.Height);}
   var location=new[]{new PointF(X+(CW-width)/2,220)};using(var transform=g.Transform)transform.TransformPoints(location);var state=g.Save();g.ResetTransform();g.DrawImageUnscaled(keyboardSurface,(int)Math.Round(location[0].X),(int)Math.Round(location[0].Y));g.Restore(state);
  }
  else{
   Section(g,"开始拍摄",228);Body(g,"启动游戏并进入场景 → 注入相机 → 按 Insert 开启镜头。",272);
   Line(g,X,336,X+CW,336);Section(g,"结束拍摄",366);Body(g,"按 Insert 关闭相机，恢复游戏界面与正常时间。",410);
   Line(g,X,474,X+CW,474);Section(g,"取景提示",504);Body(g,"7 / 9 沿世界竖直方向升降。F5 复位时恢复横屏。\n竖屏拍摄后，将图片或视频反向旋转 90°。",548,78);
  }
 }
 void Notes(Graphics g){
  string[] titles={"运行环境","存档与备份","模组兼容","更新与退出"};
  string[] bodies={"Windows x64 · Dark Souls III 1.15.2.0 · .NET Framework 4.x\n仅离线使用。","存档由游戏启动配置决定，测试前请备份。","已通过 Mod Engine 2 实测，其他镜头、UI或时间模组可能冲突。\n隐藏菜单后仍可操作，使用菜单前请恢复显示。","更新程序前请退出游戏。\n“恢复并断开”可恢复相机、界面与时间状态。"};
  float gap=(ViewH-252)/4;
  for(int i=0;i<4;i++){float y=166+i*gap;Section(g,titles[i],y);Body(g,bodies[i],y+40,58);if(i<3)Line(g,X,y+gap-18,X+CW,y+gap-18);}
 }
 void DrawGameSignature(Graphics g){
  if(H<640)return;
  if(signatureSurface==null){signatureSurface=new Bitmap(D(224),D(200),PixelFormat.Format32bppPArgb);using(var layer=Graphics.FromImage(signatureSurface)){layer.ScaleTransform(scale,scale);PaintGameSignature(layer,200);}}
  var state=g.Save();g.ResetTransform();g.DrawImageUnscaled(signatureSurface,0,D(H-200));g.Restore(state);
 }
 void PaintGameSignature(Graphics g,float height){
  const float gray=.68f,opacity=.88f;
  // Colour the prepared silhouette, preserving its edge alpha; reused in the signature layer.
  using(var flame=new Bitmap(D(52),D(73),PixelFormat.Format32bppPArgb)){
   using(var canvas=Graphics.FromImage(flame))using(var attributes=new ImageAttributes()){
    attributes.SetColorKey(Color.FromArgb(50,50,50),Color.White);var matrix=new ColorMatrix();matrix.Matrix00=matrix.Matrix11=matrix.Matrix22=0;
    matrix.Matrix40=matrix.Matrix41=matrix.Matrix42=gray;matrix.Matrix33=opacity;attributes.SetColorMatrix(matrix);
    canvas.DrawImage(gameBonfire,new Rectangle(0,0,flame.Width,flame.Height),250,25,350,490,GraphicsUnit.Pixel,attributes);
   }
   for(int y=0;y<flame.Height;y++){
    float row=y*73f/flame.Height,blend=Math.Max(0,Math.Min(1,(row-27)/14f))*Math.Max(0,Math.Min(1,(58-row)/9f))*.85f;
    if(blend==0)continue;
    for(int x=0;x<flame.Width;x++){Color c=flame.GetPixel(x,y);if(c.A==0)continue;
     flame.SetPixel(x,y,Color.FromArgb(c.A,Math.Min(255,(int)(c.R*(gray+(.69f-gray)*blend)/gray)),Math.Min(255,(int)(c.G*(gray+(.36f-gray)*blend)/gray)),Math.Min(255,(int)(c.B*(gray+(.20f-gray)*blend)/gray))));
    }
   }
   g.DrawImage(flame,new RectangleF(86,height-182,52,73));
  }
  using(var attributes=new ImageAttributes()){
   attributes.SetColorKey(Color.Black,Color.FromArgb(24,24,24));var matrix=new ColorMatrix();matrix.Matrix00=matrix.Matrix11=matrix.Matrix22=.72f;matrix.Matrix33=.85f;attributes.SetColorMatrix(matrix);
   g.DrawImage(gameWordmark,new Rectangle(36,(int)(height-95),152,17),19,168,1166,127,GraphicsUnit.Pixel,attributes);
  }
 }
 protected override void Dispose(bool disposing){if(disposing){animationTimer.Stop();animationTimer.Dispose();repositoryTip.Dispose();map.Dispose();textCache.Dispose();if(keyboardSurface!=null)keyboardSurface.Dispose();if(signatureSurface!=null)signatureSurface.Dispose();if(markSurface!=null)markSurface.Dispose();if(bodySurface!=null)bodySurface.Dispose();if(mark!=null)mark.Dispose();if(gameWordmark!=null)gameWordmark.Dispose();if(gameBonfire!=null)gameBonfire.Dispose();}base.Dispose(disposing);}
 void ValidateLayout(){
  if(CW>960.1f||CW<1||X<224||Math.Abs((X-224)-(W-X-CW))>.1f)throw new InvalidOperationException("Content width or centering failed");
  foreach(var rect in textBoxes.Concat(hits.Select(h=>h.Bounds)).Where(r=>r.Width>0&&r.Height>0))if(rect.Left<0||rect.Top<0||rect.Right>W+.6f||rect.Bottom>H+.6f)throw new InvalidOperationException("Text or interactive area outside viewport: "+rect);
  if(page==4&&tab==0){if(map.Bottom>D(ViewH-36-contentScroll)||map.Left<D(224)||map.Right>ClientSize.Width-D(32))throw new InvalidOperationException("Keyboard layout clipped");if(Math.Abs(map.Width/(float)map.Height-829f/552)>.005f)throw new InvalidOperationException("Keyboard layout stretched");}
 }
 void SavePreview(string path){using(var bitmap=new Bitmap(Width,Height))using(var g=Graphics.FromImage(bitmap)){
  OnPaint(new PaintEventArgs(g,new Rectangle(Point.Empty,bitmap.Size)));
  ValidateLayout();
  bitmap.Save(path);
 }}
 public static void Render(){
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview");Directory.CreateDirectory(root);
  using(var f=new PreviewForm()){f.staticRender=true;f.ShowInTaskbar=false;f.Opacity=0;f.Show();Application.DoEvents();
   var scenarios=new[]{new KeyValuePair<string,Size>("default",new Size(f.D(1200),f.D(800))),new KeyValuePair<string,Size>("minimum",new Size(f.D(840),f.D(520))),new KeyValuePair<string,Size>("1920x1080",new Size(1920,1080)),new KeyValuePair<string,Size>("2560x1440",new Size(2560,1440)),new KeyValuePair<string,Size>("3440x1440",new Size(3440,1440))};
   float renderedScale=f.scale;
   var report=new List<string>{"DPI scale: "+renderedScale.ToString(System.Globalization.CultureInfo.InvariantCulture),"Date: "+DateTimeOffset.Now.ToString("o"),"Shared production UI rendered without connecting to a game or writing configuration."};
   foreach(var scenario in scenarios){f.ClientSize=scenario.Value;Application.DoEvents();
    for(int i=0;i<6;i++){f.ShowPage(i);f.tab=0;f.Invalidate();Application.DoEvents();f.SavePreview(Path.Combine(root,scenario.Key+"-"+i+".png"));}
    report.Add(scenario.Key+": "+f.ClientSize+"; content width="+f.CW+"; centered; all six pages clipped to content viewport; keyboard aspect preserved");
   }
   f.ToggleMaximize();Application.DoEvents();if(f.WindowState!=FormWindowState.Maximized)throw new InvalidOperationException("Maximize failed");
   for(int i=0;i<6;i++){f.ShowPage(i);f.tab=0;f.Invalidate();Application.DoEvents();f.SavePreview(Path.Combine(root,"maximized-"+i+".png"));}
   report.Add("Actual maximized client: "+f.ClientSize+"; work area="+Screen.FromControl(f).WorkingArea+"; all six pages checked");
   f.ToggleMaximize();f.ClientSize=new Size(f.D(1200),f.D(800));f.page=4;f.tab=1;f.Invalidate();Application.DoEvents();f.SavePreview(Path.Combine(root,"default-help.png"));
   f.page=0;f.gameAvailable=true;f.pointer=new PointF(f.X+80,438+f.BodyOffset);f.SavePreview(Path.Combine(root,"default-hover.png"));
   f.ShowPage(3);f.connected=true;f.following=true;f.hidden=true;f.slowed=true;f.source=1;f.orientation=1;f.pointer=new PointF(-1,-1);f.SavePreview(Path.Combine(root,"default-active.png"));
   f.keyboardFocus=true;f.focusIndex=f.hits.FindIndex(h=>h.Name=="人物移动方向参考");f.SavePreview(Path.Combine(root,"default-focus.png"));
   f.ShowPage(1);f.selectedKey=8;f.RestoreDefaultBindings();f.SavePreview(Path.Combine(root,"default-keys-pending.png"));
   DarkDialog.RenderExamples(root);
   report.Add("Hover, disabled/enabled, segments, focus, pending default bindings and help layouts rendered; dialogs checked at 100/125/150/200% DPI.");
   foreach(float factor in new[]{1f,1.25f,1.5f,2f})using(var sample=new PreviewForm(factor)){
    sample.staticRender=true;sample.ClientSize=new Size(sample.D(900),sample.D(520));sample.ShowInTaskbar=false;sample.Opacity=0;sample.Show();Application.DoEvents();
    for(int i=0;i<6;i++){sample.ShowPage(i);sample.SavePreview(Path.Combine(root,"compact-"+factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+i+".png"));sample.contentScroll=i==1?0:sample.ViewH-sample.H;sample.SavePreview(Path.Combine(root,"compact-bottom-"+factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+i+".png"));}
   }
   report.Add("All six compact pages and scroll endpoints checked at 100/125/150/200% DPI; clicks clipped to visible content.");
   KeyboardMap.SaveImage(Path.Combine(root,"default-keybindings.png"));
   File.WriteAllLines(Path.Combine(root,"layout-check.txt"),report.ToArray());f.Close();
  }
 }
}
}
