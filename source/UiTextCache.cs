using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
// Reuse immutable native-text sprites: no per-frame HDC readback or font creation.
// Bound storage across resize, changing numeric values and animated foreground colours.
sealed class UiTextCache:IDisposable {
 sealed class Entry {public Bitmap Image;public int Pixels;public Point Offset;}
 readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();readonly Queue<string> order=new Queue<string>();
 readonly Dictionary<int,Font> fonts=new Dictionary<int,Font>();int pixels;
 const int MaxPixels=8*1024*1024,MaxEntries=256;
 public void Draw(Graphics target,string text,Rectangle rect,int size,bool bold,Color color,TextFormatFlags flags){
  if(rect.Width<=0||rect.Height<=0||string.IsNullOrEmpty(text))return;
  int rgb=Color.FromArgb(Math.Min(255,(color.R+2)/4*4),Math.Min(255,(color.G+2)/4*4),Math.Min(255,(color.B+2)/4*4)).ToArgb();
  string key=text+"\u001f"+rect.Width+","+rect.Height+","+size+","+bold+","+rgb+","+(int)flags;Entry entry;
  if(!entries.TryGetValue(key,out entry)){
   int fontKey=size*2+(bold?1:0);Font font;if(!fonts.TryGetValue(fontKey,out font)){font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);fonts.Add(fontKey,font);}
   int count=checked(rect.Width*rect.Height);var sprite=new Bitmap(rect.Width,rect.Height,PixelFormat.Format32bppPArgb);
   using(var g=Graphics.FromImage(sprite)){g.Clear(Color.Black);TextRenderer.DrawText(g,text,font,new Rectangle(Point.Empty,rect.Size),Color.White,flags);}
   int left=rect.Width,top=rect.Height,right=-1,bottom=-1;
   var bits=sprite.LockBits(new Rectangle(Point.Empty,sprite.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
   try{int[] row=new int[rect.Width];Color ink=Color.FromArgb(rgb);for(int y=0;y<rect.Height;y++){
    IntPtr address=IntPtr.Add(bits.Scan0,y*bits.Stride);Marshal.Copy(address,row,0,row.Length);
    for(int x=0;x<row.Length;x++){int value=row[x];int coverage=((value&255)+((value>>8)&255)+((value>>16)&255))/3;if(coverage>0){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=y;}row[x]=(coverage<<24)|((ink.R*coverage/255)<<16)|((ink.G*coverage/255)<<8)|(ink.B*coverage/255);}
    Marshal.Copy(row,0,address,row.Length);
   }}finally{sprite.UnlockBits(bits);}
   var crop=right<left?new Rectangle(0,0,1,1):Rectangle.FromLTRB(left,top,right+1,bottom+1);var trimmed=sprite.Clone(crop,PixelFormat.Format32bppPArgb);sprite.Dispose();sprite=trimmed;count=crop.Width*crop.Height;
   while(order.Count>0&&(entries.Count>=MaxEntries||pixels+count>MaxPixels)){string expired=order.Dequeue();Entry old=entries[expired];pixels-=old.Pixels;old.Image.Dispose();entries.Remove(expired);}
   entry=new Entry{Image=sprite,Pixels=count,Offset=crop.Location};entries.Add(key,entry);order.Enqueue(key);pixels+=count;
  }
  target.DrawImageUnscaled(entry.Image,rect.X+entry.Offset.X,rect.Y+entry.Offset.Y);
 }
 public void Dispose(){foreach(var e in entries.Values)e.Image.Dispose();foreach(var f in fonts.Values)f.Dispose();entries.Clear();fonts.Clear();order.Clear();pixels=0;}
}
}
