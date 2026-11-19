using System;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
// Shared view: the base remains an isolated preview; ClientForm supplies runtime actions.
public class PreviewForm:Form {
 readonly Color background=Color.FromArgb(5,5,5),sidebar=Color.FromArgb(46,46,46),muted=Color.FromArgb(168,168,168),control=Color.FromArgb(47,47,47);
 readonly float dpi=PreviewDpi.Scale;int D(int value){return (int)Math.Round(value*dpi);}
 bool showHelpSteps;Panel title,nav,content,footer;Button[] navigation;protected Label status;protected Settings settings;protected DataGridView keyGrid;protected ComboBox direction;protected CheckBox framing;protected TextBox processField;protected Button injectButton;protected readonly Dictionary<string,NumericUpDown> numbers=new Dictionary<string,NumericUpDown>();
 protected int currentPage;protected string loadError="";
 readonly string[] pages={"连接与注入","按键设置","速度与手感","UI、暂停与竖屏","操作说明","注意事项"};
 [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 public PreviewForm(){
  Text="Dark Souls III Camera · 黑暗之魂Ⅲ摄影工具";FormBorderStyle=FormBorderStyle.None;AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(D(1200),D(800));MinimumSize=new Size(D(1000),D(710));StartPosition=FormStartPosition.CenterScreen;BackColor=background;ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",12);DoubleBuffered=true;
  try{settings=Settings.Load(Path.Combine(Log.Root,"camera-settings.xml"));}catch(Exception e){loadError="配置读取失败，原文件保留："+e.Message;settings=new Settings();}
  title=new PreviewDpiPanel(dpi){BackColor=Color.Black};nav=new PreviewDpiPanel(dpi){BackColor=sidebar};content=new PreviewDpiPanel(dpi){BackColor=background,AutoScroll=true};footer=new PreviewDpiPanel(dpi){BackColor=Color.FromArgb(28,28,28)};
  Controls.AddRange(new Control[]{title,nav,content,footer});
  AddLabel(title,"Dark Souls III Camera · 黑暗之魂Ⅲ摄影工具",15,8,650,28,11,Color.White);
  title.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}};
  Button minimize=ButtonAt(title,"—",0,0,48,40,()=>WindowState=FormWindowState.Minimized),maximize=ButtonAt(title,"□",0,0,48,40,()=>WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized),close=ButtonAt(title,"×",0,0,48,40,Close);
  minimize.BackColor=maximize.BackColor=close.BackColor=Color.Black;close.FlatAppearance.MouseOverBackColor=Color.FromArgb(160,30,30);
  navigation=new Button[pages.Length];string[] icons={"◇","⌨","↗","▣","?","!"};
  for(int i=0;i<pages.Length;i++){int index=i;var button=ButtonAt(nav,icons[i]+"   "+pages[i],0,12+i*58,250,54,()=>ShowPage(index));button.TextAlign=ContentAlignment.MiddleLeft;button.Padding=new Padding(D(15),0,0,0);button.Font=new Font(Font.FontFamily,13);navigation[i]=button;}
  AddLabel(nav,"DARK SOULS III",20,390,210,25,9,muted);AddLabel(nav,"黑暗之魂 Ⅲ · 自由镜头",20,418,225,24,9,muted);
  status=AddLabel(footer,"未连接",15,6,540,24,9,muted);
  Resize+=(s,e)=>{int width=ClientSize.Width,height=ClientSize.Height;title.SetBounds(D(1),D(1),width-D(2),D(40));nav.SetBounds(D(1),D(41),D(250),height-D(76));content.SetBounds(D(251),D(41),width-D(252),height-D(76));footer.SetBounds(D(1),height-D(35),width-D(2),D(34));minimize.Left=width-D(146);maximize.Left=width-D(98);close.Left=width-D(50);status.Width=width-D(30);};
  OnResize(EventArgs.Empty);ShowPage(0);
 }
 Label AddLabel(Control parent,string text,int x,int y,int width,int height,float size,Color color){var label=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),Font=new Font(Font.FontFamily,size<12?size+2:size<20?size+1:size),ForeColor=color};parent.Controls.Add(label);return label;}
 Button ButtonAt(Control parent,string text,int x,int y,int width,int height,Action action){var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,height),FlatStyle=FlatStyle.Flat,BackColor=control,ForeColor=Color.White,TabStop=false};button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(65,65,65);button.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"操作未完成");}};parent.Controls.Add(button);return button;}
 TextBox Field(string value,int y,int width=790){var box=new TextBox{Text=value,ReadOnly=true,Location=new Point(34,y),Size=new Size(width,38),BackColor=background,ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=new Font(Font.FontFamily,13),Margin=new Padding(8)};content.Controls.Add(box);return box;}
 void Heading(string text,int y=26){AddLabel(content,text,34,y,820,52,21,Color.White);}
 void Note(string text,int y,int height=55){AddLabel(content,text,34,y,820,height,10,muted);}
 protected virtual void OnInject(){} protected virtual void OnDisconnect(){} protected virtual void OnCommand(uint command){} protected virtual void OnRefresh(){if(processField!=null)processField.Text="尚未检测到游戏进程";} protected virtual void OnSave(){} protected virtual void OnCaptureKey(){} protected virtual void OnDirection(int index){}
 protected void SetStatus(string text){status.Text=text;}
 void NumericPreview(string label,double value,int y,decimal min,decimal max,int decimals,string suffix=""){
  AddLabel(content,label,34,y+5,365,34,12,Color.White);var input=new NumericUpDown{Value=(decimal)value,Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals>=4?.0001m:decimals==2?.01m:1m,Location=new Point(420,y),Size=new Size(175,34),BackColor=background,ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=new Font(Font.FontFamily,13)};content.Controls.Add(input);numbers[label]=input;AddLabel(content,suffix,612,y+5,190,32,11,muted);
 }
 protected void ReadSettingsPage(){if(currentPage==2&&numbers.Count==6){settings.MovementSpeed=(double)numbers["相机移动速度"].Value;settings.RotationSpeed=(double)numbers["旋转速度"].Value;settings.MouseSensitivity=(double)numbers["鼠标灵敏度"].Value;settings.Smoothing=(double)numbers["平滑响应"].Value;settings.FastMultiplier=(double)numbers["按住加速倍率"].Value;settings.SlowMultiplier=(double)numbers["战斗减速倍率"].Value;}if(currentPage==3&&framing!=null)settings.PortraitKeepVerticalFraming=framing.Checked;}
 protected void RefreshKeys(){if(keyGrid==null)return;int row=keyGrid.CurrentRow==null?0:keyGrid.CurrentRow.Index;keyGrid.Rows.Clear();foreach(var binding in settings.Bindings)keyGrid.Rows.Add(ActionName(binding.Action),Settings.Name(binding.Key));if(keyGrid.Rows.Count>0)keyGrid.CurrentCell=keyGrid.Rows[Math.Min(row,keyGrid.Rows.Count-1)].Cells[0];}
 public void ShowPage(int index){
  currentPage=index;keyGrid=null;direction=null;framing=null;processField=null;injectButton=null;numbers.Clear();
  foreach(Control child in content.Controls.Cast<Control>().ToArray())child.Dispose();content.Controls.Clear();content.AutoScrollPosition=Point.Empty;for(int i=0;i<navigation.Length;i++){navigation[i].BackColor=i==index?Color.Black:sidebar;navigation[i].ForeColor=i==index?Color.White:Color.FromArgb(190,190,190);}
  if(index==0){
   Heading("连接与注入");AddLabel(content,"游戏进程",34,109,800,28,12,Color.White);processField=Field("尚未检测到游戏进程",145,670);ButtonAt(content,"刷新进程",718,143,145,37,OnRefresh);
   AddLabel(content,"相机组件",34,202,800,28,12,Color.White);Field(@".\DS3CameraCore.dll",238,829);
   injectButton=ButtonAt(content,"注入相机",34,312,150,44,OnInject);ButtonAt(content,"恢复并断开",201,312,170,44,OnDisconnect);OnRefresh();
  }else if(index==1){
   Heading("按键设置");
   var grid=new DataGridView{Location=new Point(34,143),Size=new Size(829,405),ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,BackgroundColor=background,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,EnableHeadersVisualStyles=false,GridColor=control};
   grid.Columns.Add("action","操作");grid.Columns.Add("key","绑定按键");grid.ColumnHeadersDefaultCellStyle.BackColor=control;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersHeight=38;grid.DefaultCellStyle.BackColor=background;grid.DefaultCellStyle.ForeColor=Color.White;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(50,50,50);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.RowTemplate.Height=32;
   foreach(var binding in settings.Bindings)grid.Rows.Add(ActionName(binding.Action),Settings.Name(binding.Key));content.Controls.Add(grid);grid.MultiSelect=false;keyGrid=grid;ButtonAt(content,"修改选中按键",34,576,190,43,OnCaptureKey);ButtonAt(content,"保存并应用",242,576,170,43,OnSave);
  }else if(index==2){
   Heading("速度与手感");NumericPreview("相机移动速度",settings.MovementSpeed,116,.01m,100,2,"单位 / 秒");NumericPreview("旋转速度",settings.RotationSpeed,177,.01m,10,2,"弧度 / 秒");NumericPreview("鼠标灵敏度",settings.MouseSensitivity,238,.00001m,.05m,5);NumericPreview("平滑响应",settings.Smoothing,299,0,1,2,"秒");NumericPreview("按住加速倍率",settings.FastMultiplier,360,1,50,1,"倍");NumericPreview("战斗减速倍率",settings.SlowMultiplier,421,.05m,1,2,"倍");
   ButtonAt(content,"保存并应用",34,556,190,44,OnSave);
  }else if(index==3){
   Heading("UI、暂停与竖屏");AddLabel(content,"画面与游戏状态",34,111,820,31,12,Color.White);ButtonAt(content,"隐藏 / 显示 UI",34,160,255,46,()=>OnCommand(256));ButtonAt(content,"暂停 / 继续",309,160,270,46,()=>OnCommand(512));ButtonAt(content,"减速 / 正常",599,160,264,46,()=>OnCommand(1024));
   AddLabel(content,"画面方向",34,256,820,31,12,Color.White);direction=new ComboBox{Location=new Point(34,303),Size=new Size(829,36),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=control,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};direction.Items.AddRange(new object[]{"横屏 0°","画面顺时针 90°","画面逆时针 90°"});direction.SelectedIndex=0;direction.SelectionChangeCommitted+=(s,e)=>OnDirection(direction.SelectedIndex);content.Controls.Add(direction);
   framing=new CheckBox{Text="竖屏保持上下构图，自动补偿 FOV",Location=new Point(34,366),AutoSize=true,ForeColor=Color.White,Checked=settings.PortraitKeepVerticalFraming};content.Controls.Add(framing);ButtonAt(content,"保存并应用",34,437,190,44,OnSave);
  }else if(index==4){
   Heading("操作说明");ButtonAt(content,"默认按键",34,90,120,34,()=>{showHelpSteps=false;ShowPage(4);});ButtonAt(content,"使用流程",165,90,120,34,()=>{showHelpSteps=true;ShowPage(4);});AddLabel(content,"自定义绑定请查看「按键设置」。",310,98,550,25,10,muted);
   if(!showHelpSteps)content.Controls.Add(new KeyboardMap{Location=new Point(34,143)});
   else{
    AddLabel(content,"开始拍摄",34,157,820,32,15,Color.White);AddLabel(content,"1. 启动游戏，进入可操作的场景。\n2. 在连接与注入页面点击“注入相机”。\n3. 按 Insert 开启自由镜头。",34,204,820,99,12,Color.White);
    AddLabel(content,"拍摄中",34,334,820,32,15,Color.White);AddLabel(content,"Delete 隐藏游戏界面；小键盘 0 暂停或继续。\nF9 切换战斗减速；小键盘 . 按住加速镜头。\nF6 切换横屏与竖屏；F5 重置到人物附近。",34,380,820,99,12,Color.White);
    AddLabel(content,"结束拍摄",34,507,820,32,15,Color.White);AddLabel(content,"按 Insert 关闭相机，并恢复游戏界面与正常时间。",34,552,820,42,12,Color.White);
    AddLabel(content,"操作补充",34,620,820,32,15,Color.White);
    AddLabel(content,"按键设置：选中操作，修改绑定后保存并应用。\n减速影响人物与首领，相机移动和转向速度保持不变。\n暂停后继续游戏，会保留此前的减速状态。\n鼠标与左右平移按眼前画面操作；小键盘 7 / 9 沿世界竖直方向升降。\nF5 回到人物附近并恢复横屏；竖屏截图或录像后，将画面反向旋转 90°。\nF12 使用 Steam 截图。",34,665,820,205,12,Color.White);
   }
  }else{
   Heading("注意事项");
   AddLabel(content,"运行环境",34,112,820,32,15,Color.White);
   AddLabel(content,"仅用于离线游戏，支持 Dark Souls III 1.15.2.0。\n先使用你的游戏或模组启动方式进入场景，再注入相机。",34,157,820,78,12,Color.White);
   AddLabel(content,"存档与备份",34,235,820,32,15,Color.White);
   AddLabel(content,"相机不会自动切换、复制或修改存档文件。\n使用哪个存档由游戏启动配置决定；离线不等于独立存档。\n需要隔离主存档时，请配置独立存档，并在测试前备份。",34,280,820,95,12,Color.White);
   AddLabel(content,"模组兼容",34,385,820,32,15,Color.White);
   AddLabel(content,"当前功能版本已由用户通过 Mod Engine 2 实测正常。\n其他模组组合仍需验证；修改相同镜头、UI或时间功能时可能冲突。\n隐藏菜单仅隐藏绘制，菜单仍可能响应操作；操作前先恢复显示。",34,430,820,95,12,Color.White);
   AddLabel(content,"更新与退出",34,535,820,32,15,Color.White);
   AddLabel(content,"更新相机组件前，请退出游戏。\n“恢复并断开”会恢复本工具接管的状态；组件在游戏退出时卸载。\n异常信息记录在 data/client.log。",34,580,820,110,12,Color.White);
  }
 }

 static string ActionName(string action){switch(action){case "相机开关":return "开启 / 关闭相机";case "回到人物附近":return "重置到人物附近";case "倾斜复位":return "重置镜头倾斜";case "FOV复位":return "重置视野";case "缩小FOV":return "拉近画面（缩小视野）";case "增大FOV":return "拉远画面（扩大视野）";case "隐藏或显示UI":return "隐藏 / 显示游戏界面";case "暂停或继续":return "暂停 / 继续游戏";case "减缓时间或恢复":return "战斗减速 / 正常时间";case "切换竖屏":return "切换横屏 / 竖屏";default:return action;}}
 public static void Render(){Directory.CreateDirectory(Path.Combine(Log.Root,"preview"));using(var form=new PreviewForm()){form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();for(int i=0;i<6;i++){form.ShowPage(i);Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Log.Root,"preview","page-"+i+".png"));}}form.showHelpSteps=true;form.ShowPage(4);Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Log.Root,"preview","操作说明-使用流程.png"));}form.Close();}KeyboardMap.SaveImage(Path.Combine(Log.Root,"preview","默认按键说明.png"));}
}
}
