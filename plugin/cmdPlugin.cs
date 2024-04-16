using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
 
using System.Diagnostics;

//このDllをプラグインとして認識させる
[assembly: PlugInAttribute.PlugInAssemblyAttribute]
namespace cmdPlugin
{
	[PlugInAttribute.PlugInClass("doMain",1,1)]
	public class cmdPlugin
	{

		public void doMain(){
			MainWindow cmdForm = new MainWindow();
			cmdForm.Show();

		}
	}
	public partial class MainWindow : Form
	{
		public static int foWid;
		public static int foHei;

		ListBox uxResult;
		Button button;
		TextBox uxCommand;
		public MainWindow(){
			this.Text = "cmd - inefficiency";
			this.MinimumSize = new Size(500,300);

			foWid = this.Size.Width;
			foHei = this.Size.Height;

			uxResult = new ListBox();
			this.Controls.Add(uxResult);
			uxResult.SetBounds(0, 40, foWid-20, foHei-90, BoundsSpecified.All);
			uxResult.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

			button = new Button();
			this.Controls.Add(button);
			button.Text = "post";
			button.SetBounds(foWid-110, 0, 80, 35, BoundsSpecified.All);
			button.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			button.Click += Button_Click;

			uxCommand = new TextBox();
			this.Controls.Add(uxCommand);
			uxCommand.SetBounds(0, 0, foWid-110, 40, BoundsSpecified.All);
			uxCommand.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			
		}

		private void Button_Click(object sender, EventArgs e)
		{
			uxResult.Items.Clear();

			foreach(var line in RunCommand(uxCommand.Text))
			{
				uxResult.Items.Add(line);
			}
		}

		public IEnumerable<string> RunCommand(string command)
		{
			ProcessStartInfo psInfo = new ProcessStartInfo();
			psInfo.FileName = "cmd"; // 実行するファイル
			psInfo.Arguments = "/c " + command;//引数
			psInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
			psInfo.UseShellExecute = false; // シェル機能を使用しない
			psInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト
 
			Process p = Process.Start(psInfo); // アプリの実行開始
 
			string line;
 
			while ((line = p.StandardOutput.ReadLine()) != null)
			{
				yield return line;
			}
		}
	}
}