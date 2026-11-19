using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace DS3CameraDLLTrial {
// Isolated host: deliberately excludes functional client, bridge and DLL code.
public static class Log {public static string Root=AppDomain.CurrentDomain.BaseDirectory;}
public static class PreviewHost {
 [STAThread]public static void Main(string[] args){PreviewDpi.Enable();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Contains("--render"))PreviewForm.Render();else Application.Run(new PreviewForm());}
}
}
