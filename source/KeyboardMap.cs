using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
// Physical keyboard layout drawn in the same palette and type hierarchy as the form.
public sealed class KeyboardMap:Control {
 const int DiagramWidth=829,DiagramHeight=552;
 static readonly Color surface=Color.FromArgb(5,5,5),key=Color.FromArgb(24,24,24),edge=Color.FromArgb(45,45,45),ink=Color.FromArgb(238,238,238),muted=Color.FromArgb(164,164,164),accent=Color.FromArgb(199,143,110);
 public KeyboardMap(){DoubleBuffered=true;BackColor=surface;Size=new Size(DiagramWidth,DiagramHeight);SetStyle(ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Draw(e.Graphics,ClientSize.Width,ClientSize.Height);}
 static void Label(Graphics g,string text,RectangleF box,float size,Color color,bool left=false){
  var m=g.Transform.Elements;var device=new Rectangle((int)Math.Round(box.X*m[0]+m[4]),(int)Math.Round(box.Y*m[3]+m[5]),(int)Math.Round(box.Width*m[0]),(int)Math.Round(box.Height*m[3]));
  var state=g.Save();g.ResetTransform();using(var f=new Font("Microsoft YaHei UI",(float)Math.Round(size*m[3]),FontStyle.Regular,GraphicsUnit.Pixel))TextRenderer.DrawText(g,text,f,device,color,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|(left?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));g.Restore(state);
 }
 static void Key(Graphics g,string caption,string action,int x,int y,int width=104,int height=80,bool movement=false,bool main=false){
  using(var b=new SolidBrush(main?Color.FromArgb(39,30,24):key))g.FillRectangle(b,x,y,width,height);
  using(var p=new Pen(main?Color.FromArgb(94,63,44):edge,1))g.DrawRectangle(p,x+.5f,y+.5f,width-1,height-1);
  Label(g,caption,new RectangleF(x+6,y+5,width-12,height*.49f),caption.Length>3?20:26,movement||main?accent:ink);
  Label(g,action,new RectangleF(x+6,y+height*.51f,width-12,height*.42f),14,muted);
 }
 public static void Draw(Graphics g,int width,int height){
  var saved=g.Save();g.ScaleTransform(width/(float)DiagramWidth,height/(float)DiagramHeight);using(var b=new SolidBrush(surface))g.FillRectangle(b,0,0,DiagramWidth,DiagramHeight);g.SmoothingMode=SmoothingMode.AntiAlias;
  Key(g,"F5","人物附近复位",0,0,138,72);Key(g,"F6","横屏 / 竖屏",146,0,138,72);Key(g,"F9","减速 / 正常",292,0,138,72);Key(g,"F12","Steam 截图",438,0,138,72);
  Label(g,"默认键位",new RectangleF(612,5,217,26),15,ink,true);Label(g,"自定义绑定见按键设置",new RectangleF(612,34,217,30),13,muted,true);
  Key(g,"Insert","相机开关",0,105,104,80,false,true);Key(g,"Home","锁定 / 解锁",112,105,104,80);Key(g,"Delete","隐藏 / 显示 UI",0,193,216,80);
  Key(g,"↑","向上转",74,320,68,72);Key(g,"←","向左转",0,400,68,72);Key(g,"↓","向下转",74,400,68,72);Key(g,"→","向右转",148,400,68,72);
  Label(g,"方向键与鼠标均可转向",new RectangleF(0,487,216,38),13,muted);
  const int left=244,top=105,gap=112,row=88;
  Key(g,"*","视野复位",left+gap*2,top);Key(g,"−","拉近画面",left+gap*3,top);
  Key(g,"7","世界上升",left,top+row);Key(g,"8","前进",left+gap,top+row,104,80,true);Key(g,"9","世界下降",left+gap*2,top+row);Key(g,"+","拉远画面",left+gap*3,top+row,104,168);
  Key(g,"4","左移",left,top+row*2,104,80,true);Key(g,"5","后退",left+gap,top+row*2,104,80,true);Key(g,"6","右移",left+gap*2,top+row*2,104,80,true);
  Key(g,"1","逆时针倾斜",left,top+row*3);Key(g,"2","倾斜复位",left+gap,top+row*3);Key(g,"3","顺时针倾斜",left+gap*2,top+row*3);
  Key(g,"0","暂停 / 继续",left,top+row*4,216,80);Key(g,".","按住加速",left+gap*2,top+row*4,104,80,true);
  Label(g,"鼠标",new RectangleF(713,118,116,30),15,ink,true);Label(g,"移动鼠标\n转动镜头\n\n滚轮\n拉近 / 拉远",new RectangleF(713,161,116,176),14,muted,true);
  Label(g,"7 / 9",new RectangleF(713,382,116,30),15,ink,true);Label(g,"沿世界竖直\n方向升降",new RectangleF(713,423,116,60),14,muted,true);
  g.Restore(saved);
 }
 public static void SaveImage(string path){using(var b=new Bitmap(DiagramWidth*2,DiagramHeight*2))using(var g=Graphics.FromImage(b)){Draw(g,b.Width,b.Height);b.Save(path);}}
}
}
