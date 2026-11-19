using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
public sealed class KeyboardMap:Control {
 const int DiagramWidth=829,DiagramHeight=552;
 static readonly Color surface=Color.FromArgb(7,7,7),key=Color.FromArgb(35,35,35),blue=Color.FromArgb(48,83,108),red=Color.FromArgb(139,43,43),edge=Color.FromArgb(65,65,65),muted=Color.FromArgb(187,187,187);
 public KeyboardMap(){DoubleBuffered=true;BackColor=surface;Size=new Size(DiagramWidth,DiagramHeight);SetStyle(ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Draw(e.Graphics,ClientSize.Width,ClientSize.Height);}
 static void DrawLabel(Graphics graphics,string text,RectangleF box,float size,Color color){
  var transform=graphics.Transform.Elements;var device=new Rectangle((int)Math.Round(box.X*transform[0]+transform[4]),(int)Math.Round(box.Y*transform[3]+transform[5]),(int)Math.Round(box.Width*transform[0]),(int)Math.Round(box.Height*transform[3]));
  float pixels=(float)Math.Round(size*transform[3]);var saved=graphics.Save();graphics.ResetTransform();
  using(var font=new Font("Microsoft YaHei UI",pixels,size<20?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))TextRenderer.DrawText(graphics,text,font,device,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
  graphics.Restore(saved);
 }
 static void Key(Graphics graphics,string caption,string action,int x,int y,int width=104,int height=80,Color? fill=null){
  var box=new Rectangle(x,y,width,height);using(var brush=new SolidBrush(fill??key))graphics.FillRectangle(brush,box);using(var pen=new Pen(edge,1))graphics.DrawRectangle(pen,x+.5f,y+.5f,width-1,height-1);
  if(string.IsNullOrEmpty(caption))return;
  DrawLabel(graphics,caption,new RectangleF(x+4,y+5,width-8,height*.47f),caption.Length>3&&width<130?24:30,Color.White);
  DrawLabel(graphics,action,new RectangleF(x+4,y+height*.48f,width-8,height*.46f),17,Color.FromArgb(228,228,228));
 }
 public static void Draw(Graphics graphics,int width,int height){
  var state=graphics.Save();graphics.ScaleTransform(width/(float)DiagramWidth,height/(float)DiagramHeight);graphics.Clear(surface);graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
  Key(graphics,"F5","重置到人物附近",0,0,138,78);Key(graphics,"F6","横屏 / 竖屏",146,0,138,78);Key(graphics,"F9","减速 / 正常",292,0,138,78);Key(graphics,"F12","Steam 截图",438,0,138,78);
  DrawLabel(graphics,"功能快捷键",new RectangleF(594,10,235,29),19,Color.White);DrawLabel(graphics,"自定义绑定以设置页为准",new RectangleF(594,41,235,22),16,muted);
  Key(graphics,"Insert","开启 / 关闭相机",0,105,104,92,red);Key(graphics,"Home","锁定 / 解锁",112,105,104,92);Key(graphics,"Delete","隐藏 / 显示界面",0,205,216,80);
  Key(graphics,"↑","向上转",74,320,68,72);Key(graphics,"←","向左转",0,400,68,72);Key(graphics,"↓","向下转",74,400,68,72);Key(graphics,"→","向右转",148,400,68,72);
  DrawLabel(graphics,"方向键与鼠标均可转向",new RectangleF(0,488,216,28),16,muted);
  const int left=244,top=105,gap=112,row=88;
  Key(graphics,"","",left,top);Key(graphics,"","",left+gap,top);Key(graphics,"*","视野复位",left+gap*2,top);Key(graphics,"−","拉近画面",left+gap*3,top);
  Key(graphics,"7","世界上升",left,top+row);Key(graphics,"8","前进",left+gap,top+row,104,80,blue);Key(graphics,"9","世界下降",left+gap*2,top+row);Key(graphics,"+","拉远画面",left+gap*3,top+row,104,168);
  Key(graphics,"4","左移",left,top+row*2,104,80,blue);Key(graphics,"5","后退",left+gap,top+row*2,104,80,blue);Key(graphics,"6","右移",left+gap*2,top+row*2,104,80,blue);
  Key(graphics,"1","逆时针倾斜",left,top+row*3);Key(graphics,"2","倾斜复位",left+gap,top+row*3);Key(graphics,"3","顺时针倾斜",left+gap*2,top+row*3);
  Key(graphics,"0","暂停 / 继续",left,top+row*4,216,80);Key(graphics,".","按住加速",left+gap*2,top+row*4,104,80,blue);
  DrawLabel(graphics,"鼠标",new RectangleF(704,119,125,32),20,Color.White);DrawLabel(graphics,"移动鼠标\n转动镜头\n\n滚轮\n拉近 / 拉远",new RectangleF(704,163,125,182),16,muted);
  DrawLabel(graphics,"7 / 9",new RectangleF(704,372,125,32),19,Color.White);DrawLabel(graphics,"沿世界竖直\n方向升降",new RectangleF(704,415,125,66),16,muted);
  graphics.Restore(state);
 }
 public static void SaveImage(string path){using(var bitmap=new Bitmap(DiagramWidth*2,DiagramHeight*2))using(var graphics=Graphics.FromImage(bitmap)){Draw(graphics,bitmap.Width,bitmap.Height);bitmap.Save(path);}}
}
}
