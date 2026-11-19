using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
public static class PreviewDpi {
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")]static extern uint GetDpiForSystem();
 public static void Enable(){try{SetProcessDPIAware();}catch(EntryPointNotFoundException){}}
 public static float Scale {get{try{return Math.Max(.75f,GetDpiForSystem()/96f);}catch(EntryPointNotFoundException){using(var graphics=Graphics.FromHwnd(IntPtr.Zero))return graphics.DpiX/96f;}}}
}
// The preview uses logical 96-DPI coordinates and native, unscaled text drawing.
public sealed class PreviewDpiPanel:Panel {
 readonly float factor;
 int D(int value){return (int)Math.Round(value*factor);}
 public PreviewDpiPanel(float scale){factor=scale;}
 protected override void OnControlAdded(ControlEventArgs e){
  var child=e.Control;Rectangle r=child.Bounds;
  if(child is CheckBox&&child.AutoSize)child.Location=new Point(D(r.X),D(r.Y));
  else child.SetBounds(D(r.X),D(r.Y),D(r.Width),D(r.Height));
  var grid=child as DataGridView;if(grid!=null){grid.ColumnHeadersHeight=D(grid.ColumnHeadersHeight);grid.RowTemplate.Height=D(grid.RowTemplate.Height);foreach(DataGridViewRow row in grid.Rows)row.Height=D(row.Height);}
  base.OnControlAdded(e);
 }
}
}
