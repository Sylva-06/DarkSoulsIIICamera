using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
public class DarkDialog:Form {
 protected readonly float scale;protected int D(float v){return (int)Math.Round(v*scale);}
 protected static readonly Color Bg=Color.FromArgb(17,17,17),Surface=Color.FromArgb(37,37,37),Ink=Color.FromArgb(238,238,238),Muted=Color.FromArgb(168,168,168),Edge=Color.FromArgb(63,63,63);
 [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 public DarkDialog(string caption,int width,int height):this(caption,width,height,PreviewDpi.Scale){}
 internal DarkDialog(string caption,int width,int height,float factor){
  scale=factor;Text=caption;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;ClientSize=new Size(D(width),D(height));StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;BackColor=Bg;ForeColor=Ink;DoubleBuffered=true;Font=new Font("Microsoft YaHei UI",D(14),FontStyle.Regular,GraphicsUnit.Pixel);
  var title=LabelAt(caption,20,0,width-76,44,14,Ink);title.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}};
  var close=ButtonAt("×",width-48,2,46,40,DialogResult.Cancel);close.BackColor=Bg;close.FlatAppearance.BorderSize=0;
 }
 protected Label LabelAt(string text,float x,float y,float w,float h,float size,Color color){var label=new Label{Text=text,Bounds=new Rectangle(D(x),D(y),D(w),D(h)),ForeColor=color,Font=new Font("Microsoft YaHei UI",D(size),FontStyle.Regular,GraphicsUnit.Pixel),TextAlign=ContentAlignment.MiddleLeft};Controls.Add(label);return label;}
 protected Button ButtonAt(string text,float x,float y,float w,float h,DialogResult result){var button=new DarkButton{Text=text,Bounds=new Rectangle(D(x),D(y),D(w),D(h)),BackColor=Surface,ForeColor=Ink,FlatStyle=FlatStyle.Flat,DialogResult=result};button.FlatAppearance.BorderColor=Edge;Controls.Add(button);return button;}
 internal void KeyPrompt(){var label=LabelAt("按下新按键，Esc 取消。\n录入小键盘按键前请打开 NumLock。",24,60,384,60,14,Ink);label.TextAlign=ContentAlignment.MiddleCenter;CancelButton=ButtonAt("取消",288,140,120,36,DialogResult.Cancel);}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;return true;}return base.ProcessCmdKey(ref msg,keyData);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Edge))e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1);using(var pen=new Pen(Color.FromArgb(36,36,36)))e.Graphics.DrawLine(pen,D(20),D(44),ClientSize.Width-D(20),D(44));}
 internal void CheckLayout(){foreach(Control c in Controls)if(c.Left<0||c.Top<0||c.Right>ClientSize.Width||c.Bottom>ClientSize.Height)throw new InvalidOperationException("Dialog control clipped: "+c.Text);}
 internal void SaveRender(string path){ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();CheckLayout();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}Hide();}
 public static void RenderExamples(string root){foreach(float factor in new[]{1f,1.25f,1.5f,2f}){string tag=factor.ToString("0.00",CultureInfo.InvariantCulture);using(var dialog=new NumberDialog("旋转速度",1.3,.01,10,.01,2,factor))dialog.SaveRender(Path.Combine(root,"dialog-number-"+tag+".png"));using(var dialog=new DarkDialog("录入按键",432,196,factor)){dialog.KeyPrompt();dialog.SaveRender(Path.Combine(root,"dialog-key-"+tag+".png"));}}}
}
sealed class DarkButton:Button {
 bool hover;protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(hover?Color.FromArgb(51,51,51):BackColor);using(var pen=new Pen(Focused?Color.FromArgb(183,107,66):FlatAppearance.BorderColor))if(FlatAppearance.BorderSize!=0)e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.NoPrefix|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}
public sealed class NumberDialog:DarkDialog {
 readonly TextBox field;readonly Label error;readonly double minimum,maximum,increment;readonly int decimals;
 public double Value{get;private set;}
 public NumberDialog(string caption,double value,double min,double max,double step,int digits):this(caption,value,min,max,step,digits,PreviewDpi.Scale){}
 internal NumberDialog(string caption,double value,double min,double max,double step,int digits,float factor):base(caption,384,242,factor){
  Value=value;minimum=min;maximum=max;increment=step;decimals=digits;
  LabelAt("范围 "+min.ToString(CultureInfo.InvariantCulture)+" – "+max.ToString(CultureInfo.InvariantCulture),24,54,336,22,12,Muted);
  var panel=new Panel{Bounds=new Rectangle(D(24),D(86),D(336),D(44)),BackColor=Surface};panel.Paint+=(s,e)=>{using(var pen=new Pen(panel.ContainsFocus?Color.FromArgb(183,107,66):Edge))e.Graphics.DrawRectangle(pen,0,0,panel.Width-1,panel.Height-1);};Controls.Add(panel);
  field=new TextBox{Text=value.ToString("F"+digits,CultureInfo.InvariantCulture),BorderStyle=BorderStyle.None,BackColor=Surface,ForeColor=Ink,Font=new Font("Microsoft YaHei UI",D(16),FontStyle.Regular,GraphicsUnit.Pixel),Bounds=new Rectangle(D(12),D(10),D(216),D(26))};panel.Controls.Add(field);field.GotFocus+=(s,e)=>panel.Invalidate();field.LostFocus+=(s,e)=>panel.Invalidate();
  var minus=ButtonAt("−",264,90,42,36,DialogResult.None);minus.Click+=(s,e)=>Adjust(-1);var plus=ButtonAt("+",312,90,42,36,DialogResult.None);plus.Click+=(s,e)=>Adjust(1);
  error=LabelAt("",24,136,336,32,12,Color.FromArgb(211,152,116));
  CancelButton=ButtonAt("取消",104,186,120,36,DialogResult.Cancel);var ok=ButtonAt("确定",240,186,120,36,DialogResult.None);ok.Click+=(s,e)=>TryAccept();AcceptButton=ok;Shown+=(s,e)=>{field.Focus();field.SelectAll();};
 }
 bool Read(out double value){bool parsed=double.TryParse(field.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.TryParse(field.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out value);if(!parsed||double.IsNaN(value)||double.IsInfinity(value)||value<minimum||value>maximum){error.Text="请输入范围内的有效数值。";return false;}error.Text="";return true;}
 void Adjust(int sign){double value;if(Read(out value))field.Text=Math.Max(minimum,Math.Min(maximum,Math.Round(value+sign*increment,5))).ToString("F"+decimals,CultureInfo.InvariantCulture);}
 internal bool TryAccept(){double value;if(!Read(out value))return false;Value=value;DialogResult=DialogResult.OK;return true;}
 internal TextBox Input{get{return field;}}
}
}
