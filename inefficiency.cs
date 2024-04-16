using PlugInAttribute;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;

struct Program
{
	[STAThread]
	static void Main(string[] args)
	{
		Console.WriteLine("! 起動");

		SetProcessDPIAware();
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run(new EditForm(args));
	}
	[DllImport("user32.dll")]
	private static extern bool SetProcessDPIAware();
}

/*--------------------------------------------------*/
//共通変数
/*--------------------------------------------------*/
public static class SettingDatas
{
	readonly public static string ProgramFileName = "inefficiency";
	readonly public static string mainProgramName = "テキストエディタ(仮)";
	readonly public static string mainProgramVer = "ver.0.0.1";

	//コマンドライン引数
	public static string[] cmdArg;
	//ファイルシステム
	public static string CFGP =@"data\config.cfg";
	public static string[] CFGPdata;
	public static string DFP =@"data\dlgFilter.dat";
	public static string SFP =@"data\settings.dat";
	public static string[] SFPdata;

	//Richテキスト内部設定用
	public static string PLP =@"proLang\";
	public static bool PLPdo = false;
	public static string PLPcolor = "";
	public static List<string> PLPword1 = new List<string>();
	public static List<string> PLPword2 = new List<string>();
	public static string[] PLPcommand = new string[3];
	public static string PLPoldExtension = "";

	//タイトルキャッシュ
	public static string titleCache = mainProgramName + " " + SettingDatas.mainProgramVer;
	public static string nowTitleCache = "*無題.txt - " + titleCache;

	//現在開いているファイルパス
	public static string openFilePath = "";
	public static string openFileName = "無題.txt";
	//現在開いているファイルの文字コード
	public static Encoding openFileEnc = Encoding.UTF8;
	public static string openFileEncStr = "UTF-8";
	//現在開いているファイルの改行コード
	public static string openFileRetStr = "Windows(CRLF)";

	//カーソル位置
	public static int cursorSel = 0;

	//現在未保存か?
	public static bool isSaveFile = true;
	//変更したか?(ハイライト用)
	public static bool isChangeData = true;

	//ダイアログファイル共通化
	public static string dialogFilterSet = "";
}

/*--------------------------------------------------*/
//本体
/*--------------------------------------------------*/
public class EditForm : Form
{
	//API宣言
	[DllImport("USER32.dll")]
	private static extern IntPtr SendMessage(System.IntPtr hWnd, Int32 Msg, Int32 wParam, ref Point lParam);

	/*--------------------------------------------------*/

	public static int scWid;
	public static int scHei;
	public static int foWid;
	public static int foHei;

	MenuStrip menuStrip;

	Panel TextPanel;
	protected RichTextBoxJapanese Edit_rtxb;
	
	Label RowColLabel;
	Label ZoomLevelLabel;
	Label lineFeedLabel;
	Label EncodingLabel;

	//テキストボックス上部空白サイズ
	public static int rtxb_topSize = 35;

	public EditForm(string[] args)
	{
		SettingDatas.cmdArg = args;
		Console.WriteLine("# " + SettingDatas.ProgramFileName + ".exe " + SettingDatas.mainProgramVer);

		this.Text = SettingDatas.nowTitleCache;
		this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		this.MinimumSize = new Size(500,300);

		scWid = Screen.PrimaryScreen.Bounds.Width;
		scHei = Screen.PrimaryScreen.Bounds.Height;
		foWid = this.Size.Width;
		foHei = this.Size.Height;

		//作業ディレクトリの変更
		Directory.SetCurrentDirectory(Application.StartupPath +@"\");

		//コンフィグ
		SettingDatas.CFGPdata = SettingFiles.Reading(SettingDatas.CFGP, 1);

		//フィルター設定
		string[] filters = SettingFiles.Reading(SettingDatas.DFP, 1);
		filters[0] = "すべてのファイル|*.*";
		string strFilter = String.Join("|", filters)
			.RegexReplace(@"/\*.*?\*/", String.Empty)
			.RegexReplace(@"[ 　\t]", String.Empty)
			.RegexReplace(@"\|+", "|");
		if(strFilter.Substring(strFilter.Length - 1) == "|"){
			strFilter = strFilter.Substring(0,strFilter.Length - 1);
		}
		//Console.WriteLine(strFilter);
		SettingDatas.dialogFilterSet = strFilter;

		//起動時・終了時設定
		this.Load += new EventHandler(this.EditForm_Load);
		this.Closed += new EventHandler(this.EditForm_Closed);
		//リサイズ
		this.ResizeEnd += new EventHandler(EditForm_ResizeEnd);
		//IME対応
		this.InputLanguageChanged += EditForm_InputLanguageChanged;

		CreateGUI();
	}

	private void EditForm_Load(object sender, EventArgs e){
		this.Size = new Size(int.Parse(SettingDatas.CFGPdata[0]), int.Parse(SettingDatas.CFGPdata[1]));

		Console.WriteLine("@ plugin LoadStart");
		PluginRelation.DoPlugin.ReadPlugin();
		Console.WriteLine("@ plugin LoadEnd");

		if(SettingDatas.cmdArg.Length > 0){
			openTextFile(SettingDatas.cmdArg[0]);
		}
		else{
			HighlightType("begin init", 0);
		}

		Console.WriteLine("! " + SettingDatas.ProgramFileName + " Load");
	}
	private void EditForm_Closed(object sender, EventArgs e){
		SystemRelation.EditForm_Exit();
	}

	private void EditForm_ResizeEnd(object sender, EventArgs e){
		foWid = this.Size.Width;
		foHei = this.Size.Height;

		if(int.Parse(SettingDatas.CFGPdata[0]) != foWid || int.Parse(SettingDatas.CFGPdata[1]) != foHei){
			SettingDatas.CFGPdata[0] = foWid.ToString();
			SettingDatas.CFGPdata[1] = foHei.ToString();
			SettingFiles.Update(SettingDatas.CFGP, SettingDatas.CFGPdata);
		}
	}

	/*--------------------------------------------------*/
	// GUI設定
	/*--------------------------------------------------*/
	private void CreateGUI(){

		this.SuspendLayout();

		//上のメニュー
		this.menuStrip = new MenuStrip();
		this.menuStrip.SuspendLayout();

		ToolStripMenuItem fileMenuItem = new ToolStripMenuItem();
		fileMenuItem.Text = "ファイル(&F)";
		this.menuStrip.Items.Add(fileMenuItem);
		
		ToolStripMenuItem openMenuItem = new ToolStripMenuItem();
		openMenuItem.Text = "開く(&O)...";
		openMenuItem.ShortcutKeys = Keys.Control | Keys.O;
		openMenuItem.ShowShortcutKeys = true;
		openMenuItem.Click += new EventHandler(openMenuItem_Click);
		fileMenuItem.DropDownItems.Add(openMenuItem);

		ToolStripMenuItem saveMenuItem = new ToolStripMenuItem();
		saveMenuItem.Text = "上書き保存(&S)";
		saveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
		saveMenuItem.ShowShortcutKeys = true;
		saveMenuItem.Click += new EventHandler(saveMenuItem_Click);
		fileMenuItem.DropDownItems.Add(saveMenuItem);

		ToolStripMenuItem saveAsMenuItem = new ToolStripMenuItem();
		saveAsMenuItem.Text = "名前を付けて保存(&A)...";
		saveAsMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
		saveAsMenuItem.ShowShortcutKeys = true;
		saveAsMenuItem.Click += new EventHandler(saveAsMenuItem_Click);
		fileMenuItem.DropDownItems.Add(saveAsMenuItem);

		fileMenuItem.DropDownItems.Add(new ToolStripSeparator());

		ToolStripMenuItem exitMenuItem = new ToolStripMenuItem();
		exitMenuItem.Text = SettingDatas.mainProgramName + "の終了(&X)";
		exitMenuItem.ShortcutKeys = Keys.Control | Keys.W;
		exitMenuItem.ShowShortcutKeys = true;
		exitMenuItem.Click += new EventHandler(exitMenuItem_Click);
		fileMenuItem.DropDownItems.Add(exitMenuItem);

		this.Controls.Add(this.menuStrip);
		this.MainMenuStrip = this.menuStrip;

		this.menuStrip.ResumeLayout(false);
		this.menuStrip.PerformLayout();

		/*--------------------------------------------------*/

		//パネル
		TextPanel = new Panel();
		this.Controls.Add(TextPanel);
		TextPanel.SetBounds(0, rtxb_topSize, foWid-22, foHei-rtxb_topSize-90, BoundsSpecified.All);
		TextPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		

		//Richテキストボックス
		Edit_rtxb = new RichTextBoxJapanese();
		TextPanel.Controls.Add(Edit_rtxb);
		Edit_rtxb.SetBounds(40, 0, TextPanel.Width-40, TextPanel.Height, BoundsSpecified.All);
		Edit_rtxb.Font = new Font("ＭＳ ゴシック",12);
		Edit_rtxb.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		Edit_rtxb.MaxLength = 0;
		Edit_rtxb.WordWrap = false;
		Edit_rtxb.AcceptsTab = true;
		Edit_rtxb.Text = "";
		Edit_rtxb.Focus();
		//行数計算
		Edit_rtxb.TextChanged += new EventHandler(Edit_rtxb_TextChanged);
		Edit_rtxb.VScroll += new EventHandler(Edit_rtxb_VScroll);
		//行列計算
		Edit_rtxb.MouseDown += new MouseEventHandler(Edit_rtxb_MouseDown);
		Edit_rtxb.KeyDown += new KeyEventHandler(Edit_rtxb_KeyDown);
		Edit_rtxb.KeyUp += new KeyEventHandler(Edit_rtxb_KeyUp);
		//リンク有効化
		Edit_rtxb.DetectUrls = false;
		//ドラッグ&ドロップ対応
		Edit_rtxb.AllowDrop = true;
		Edit_rtxb.DragDrop += new DragEventHandler(Edit_rtxb_DragDrop);
		Edit_rtxb.DragEnter += new DragEventHandler(Edit_rtxb_DragEnter);
		//ハイライト用
		Edit_rtxb.PreviewKeyDown += Edit_rtxb_PreviewKeyDown;


		//行列ラベル
		RowColLabel = new Label();
		this.Controls.Add(RowColLabel);
		RowColLabel.SetBounds(foWid-720, foHei-100, 201, 50, BoundsSpecified.All);
		RowColLabel.Font = new Font("ＭＳ ゴシック",11, FontStyle.Bold);
		RowColLabel.BorderStyle = BorderStyle.FixedSingle;
		RowColLabel.TextAlign = ContentAlignment.MiddleLeft;
		RowColLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		RowColLabel.Text = "1 行、1 列";

		//拡大率ラベル
		ZoomLevelLabel = new Label();
		this.Controls.Add(ZoomLevelLabel);
		ZoomLevelLabel.SetBounds(foWid-520, foHei-100, 71, 50, BoundsSpecified.All);
		ZoomLevelLabel.Font = new Font("ＭＳ ゴシック",11, FontStyle.Bold);
		ZoomLevelLabel.BorderStyle = BorderStyle.FixedSingle;
		ZoomLevelLabel.TextAlign = ContentAlignment.MiddleLeft;
		ZoomLevelLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		ZoomLevelLabel.Text = "100%";

		//改行コードラベル
		lineFeedLabel = new Label();
		this.Controls.Add(lineFeedLabel);
		lineFeedLabel.SetBounds(foWid-450, foHei-100, 201, 50, BoundsSpecified.All);
		lineFeedLabel.Font = new Font("ＭＳ ゴシック",11, FontStyle.Bold);
		lineFeedLabel.BorderStyle = BorderStyle.FixedSingle;
		lineFeedLabel.TextAlign = ContentAlignment.MiddleLeft;
		lineFeedLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		lineFeedLabel.Text = SettingDatas.openFileRetStr;

		//文字コードラベル
		EncodingLabel = new Label();
		this.Controls.Add(EncodingLabel);
		EncodingLabel.SetBounds(foWid-250, foHei-100, 250, 50, BoundsSpecified.All);
		EncodingLabel.Font = new Font("ＭＳ ゴシック",11, FontStyle.Bold);
		EncodingLabel.BorderStyle = BorderStyle.FixedSingle;
		EncodingLabel.TextAlign = ContentAlignment.MiddleLeft;
		EncodingLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		EncodingLabel.Text = SettingDatas.openFileEncStr;


		this.ResumeLayout(false);
		this.PerformLayout();

	}
	/*--------------------------------------------------*/
	// メニューのクリックイベント
	/*--------------------------------------------------*/
	private void openMenuItem_Click(object sender, EventArgs e){
		openAsMenuTextFile();
	}
	private void saveMenuItem_Click(object sender, EventArgs e){
		saveTextFile();
	}
	private void saveAsMenuItem_Click(object sender, EventArgs e){
		saveAsMenuTextFile(false);
	}
	private void exitMenuItem_Click(object sender, EventArgs e){
		SystemRelation.EditForm_Exit();
	}
	private void openAsMenuTextFile(){
		OpenFileDialog dialog = new OpenFileDialog();
		dialog.Filter = SettingDatas.dialogFilterSet;
		dialog.Title = "ファイルを開く";
		if(dialog.ShowDialog() == DialogResult.OK){
			openTextFile(dialog.FileName);
		}
	}
	private void openTextFile(string fileName){
		SettingDatas.openFilePath = fileName;
		SettingDatas.openFileName = Path.GetFileName(SettingDatas.openFilePath);
		string rtxbStr = Edit_rtxb.Text = SystemRelation.getFileData(SettingDatas.openFilePath);

		SettingDatas.isSaveFile = false;
		SettingDatas.nowTitleCache = SettingDatas.openFileName + " - " + SettingDatas.titleCache;
		this.Text = SettingDatas.nowTitleCache;
		EncodingLabel.Text = SettingDatas.openFileEncStr;

		if(rtxbStr.Contains("\r\n")){
			SettingDatas.openFileRetStr = "Windows(CRLF)";
		}
		else if(rtxbStr.Contains("\n")){
			SettingDatas.openFileRetStr = "Unix(LF)";
		}
		else if(rtxbStr.Contains("\r")){
			SettingDatas.openFileRetStr = "MacOS 9以前(CR)";
		}
		else{
			SettingDatas.openFileRetStr = "Windows(CRLF)";
		}
		lineFeedLabel.Text = SettingDatas.openFileRetStr;

		HighlightType(Path.GetExtension(SettingDatas.openFilePath).Remove(0, 1), 0);
		Edit_rtxb_Highlight();
	}
	private void saveAsMenuTextFile(bool nflag){
		SaveFileDialog dialog = new SaveFileDialog();
		dialog.FileName = SettingDatas.openFileName;
		if(SettingDatas.openFilePath != ""){
			dialog.InitialDirectory = Path.GetDirectoryName(SettingDatas.openFilePath) +@"\";
		}
		dialog.Filter = SettingDatas.dialogFilterSet;
		dialog.Title = "名前を付けて保存";
		if (dialog.ShowDialog() == DialogResult.OK){
			if(nflag){
				SettingDatas.isSaveFile = false;
				this.Text = SettingDatas.nowTitleCache;
			}
			SystemRelation.saveFileData(dialog.FileName, Edit_rtxb.Text);
		}
	}
	private void saveTextFile(){
		if(SettingDatas.openFilePath == ""){
			saveAsMenuTextFile(true);
		}
		else{
			SettingDatas.isSaveFile = false;
			this.Text = SettingDatas.nowTitleCache;
			SystemRelation.saveFileData(SettingDatas.openFilePath, Edit_rtxb.Text);
		}
	}
	/*--------------------------------------------------*/
	// ファイルのドラッグ&ドロップ
	/*--------------------------------------------------*/
	private void Edit_rtxb_DragDrop(object sender, DragEventArgs e){
		string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false );
		openTextFile(files[0]);
	}
	private void Edit_rtxb_DragEnter(object sender, DragEventArgs e){
		if (e.Data.GetDataPresent(DataFormats.FileDrop)){
			e.Effect = DragDropEffects.Copy;
		}
		else{
			e.Effect = DragDropEffects.None;
		}
	}
	/*--------------------------------------------------*/
	// テキストボックスの行番号表示
	/*--------------------------------------------------*/
	private void Edit_rtxb_VScroll(object sender, EventArgs e){
		DrawLineNumber();
		Edit_rtxb_ZoomLevel();
	}

	private void Edit_rtxb_TextChanged(object sender, EventArgs e){
		DrawLineNumber();
	}
	private void DrawLineNumber(){
		int lineNum = 0;
		int height = Edit_rtxb.Size.Height;
		Graphics g = TextPanel.CreateGraphics();
		g.Clear(Color.White);

		int charIndex = Edit_rtxb.GetCharIndexFromPosition(new Point(0, 0));
		lineNum = Edit_rtxb.GetLineFromCharIndex(charIndex);
		Font f = new Font("Consolas", 16, GraphicsUnit.Pixel);

		while(true){
			charIndex = Edit_rtxb.GetFirstCharIndexFromLine(lineNum);
			if(charIndex == -1)
				break;
			Point pt = Edit_rtxb.GetPositionFromCharIndex(charIndex);
			if(pt.Y >= 0){
				g.DrawString((lineNum + 1).ToString(), f, Brushes.Blue, new PointF(0, pt.Y+2));
			}
			lineNum++;

			if(height <= pt.Y){
				break;
			}
		}
		g.Dispose();
	}
	/*--------------------------------------------------*/
	// カレット位置の行列表示
	/*--------------------------------------------------*/
	private void Edit_rtxb_MouseDown(object sender, EventArgs e){
		printSelectRowCol();
		Edit_rtxb_ZoomLevel();

		if(!SettingDatas.isChangeData){
			SettingDatas.isChangeData = true;
			Edit_rtxb_Highlight();
		}
	}
	private void Edit_rtxb_KeyDown(object sender, KeyEventArgs e){
		printSelectRowCol();
	}
	private void Edit_rtxb_KeyUp(object sender, KeyEventArgs e){
		printSelectRowCol();
	}
	private void printSelectRowCol(){
		string str = Edit_rtxb.Text;
		//カレットの位置を取得
		int selectPos = Edit_rtxb.SelectionStart;

		//カレットの位置までの行を数える
		int row = 1, startPos = 0;
		for(int endPos=0;(endPos=str.IndexOf('\n', startPos)) < selectPos && endPos > -1;row++){
			startPos = endPos + 1;
		}

		//列の計算
		int col = selectPos - startPos + 1;

		RowColLabel.Text = row + " 行、" + col + " 列";
		SettingDatas.cursorSel = Edit_rtxb.SelectionStart;
	}
	/*--------------------------------------------------*/
	// Richテキストボックスの拡大率表示
	/*--------------------------------------------------*/
	private void Edit_rtxb_ZoomLevel(){
		ZoomLevelLabel.Text = (Edit_rtxb.ZoomFactor * 100) + "%";
	}
	/*--------------------------------------------------*/
	// RichテキストボックスIME対応
	/*--------------------------------------------------*/
	void EditForm_InputLanguageChanged(object sender, InputLanguageChangedEventArgs e){
		if (!e.InputLanguage.Culture.TwoLetterISOLanguageName.Equals("ja"))
			Edit_rtxb.JapaneseWorkaroundEnabled = false;
		else
			Edit_rtxb.JapaneseWorkaroundEnabled = true;
	}
	/*--------------------------------------------------*/
	// Richテキストボックスのソースコードハイライト
	/*--------------------------------------------------*/
	private void Edit_rtxb_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e){
		printSelectRowCol();
		switch(e.KeyCode){
			case Keys.Up:
			case Keys.Left:
			case Keys.Right:
			case Keys.Down:
				Edit_rtxb_ZoomLevel();
				if(!SettingDatas.isChangeData){
					SettingDatas.isChangeData = true;
					Edit_rtxb_Highlight();
				}
				return;
			case Keys.ControlKey:
				Edit_rtxb_ZoomLevel();
				return;
			case Keys.ShiftKey:
				return;
		}
		SettingDatas.isChangeData = false;
		if(!SettingDatas.isSaveFile){
			SettingDatas.isSaveFile = true;
			this.Text = "*" + this.Text;
		}
	}

	private void HighlightType(string extension, int makingAttempt){
		if(SettingDatas.PLPoldExtension == extension){
			return;
		}
		string[] colors = SettingFiles.Reading(SettingDatas.PLP + extension + ".icol", 2);
		if(colors != null){
			string[] word1s = SettingFiles.Reading(SettingDatas.PLP + extension + ".iwrd1", 2);
			string[] word2s = SettingFiles.Reading(SettingDatas.PLP + extension + ".iwrd2", 2);
			if(word1s != null && word2s != null){
				string[] coms = SettingFiles.Reading(SettingDatas.PLP + extension + ".icom", 2);
				if(coms != null){
					SettingDatas.PLPcolor = String.Join(";", colors)
						.RegexReplace(@"/\*.*?\*/", String.Empty)
						.RegexReplace(@"[ 　]", String.Empty)
						.RegexReplace(@"\;+", ";");
					if(SettingDatas.PLPcolor.Substring(SettingDatas.PLPcolor.Length - 1) != ";"){
						SettingDatas.PLPcolor += ";";
					}

					string Pwords = String.Join(",", word1s)
						.RegexReplace(@"/\*.*?\*/", String.Empty)
						.RegexReplace(@"[ 　\t]", String.Empty)
						.RegexReplace(@"\,+", ",");
					if(Pwords.Substring(Pwords.Length - 1) == ","){
						Pwords = Pwords.Substring(0,Pwords.Length - 1);
					}
					SettingDatas.PLPword1 = new List<string>();
					SettingDatas.PLPword1 = Pwords.Split(',').ToList();

					Pwords = String.Join(",", word2s)
						.RegexReplace(@"/\*.*?\*/", String.Empty)
						.RegexReplace(@"[ 　\t]", String.Empty)
						.RegexReplace(@"\,+", ",");
					if(Pwords.Substring(Pwords.Length - 1) == ","){
						Pwords = Pwords.Substring(0,Pwords.Length - 1);
					}
					SettingDatas.PLPword2 = new List<string>();
					SettingDatas.PLPword2 = Pwords.Split(',').ToList();

					SettingDatas.PLPcommand = coms;

					SettingDatas.PLPdo = true;
					SettingDatas.PLPoldExtension = extension;
					return;
				}
			}
		}
		if(makingAttempt == 0){
			string[] others = SettingFiles.Reading(SettingDatas.PLP + "init.idat", 1);
			if(others != null){
				string Pother = String.Join(",", others)
					.RegexReplace(@"/\*.*?\*/", String.Empty)
					.RegexReplace(@"[ 　]", String.Empty)
					.RegexReplace(@"\,+", ",");
				if(Pother.Substring(Pother.Length - 1) == ","){
					Pother = Pother.Substring(0,Pother.Length - 1);
				}
				string[] PothArr = Pother.Split(',');
				for(int i=0,li=PothArr.Length;i<li;i++){
					string[] poa = PothArr[i].Split('\t');
					if(poa[0] == extension){
						HighlightType(poa[1], 1);
						return;
					}
				}
			}
		}
		if(SettingDatas.PLPoldExtension == "init"){
			return;
		}
		colors = SettingFiles.Reading(SettingDatas.PLP + "init.icol", 1);
		if(colors != null){
			SettingDatas.PLPcolor = String.Join(";", colors)
				.RegexReplace(@"/\*.*?\*/", String.Empty)
				.RegexReplace(@"[ 　\t]", String.Empty)
				.RegexReplace(@"\;+", ";");
			if(SettingDatas.PLPcolor.Substring(SettingDatas.PLPcolor.Length - 1) != ";"){
				SettingDatas.PLPcolor += ";";
			}
			SettingDatas.PLPoldExtension = "init";

			SettingDatas.PLPcommand[0] = "#$not data#$";
			SettingDatas.PLPcommand[1] = "#$not data#$";
			SettingDatas.PLPcommand[2] = "#$not data#$\t#$not data#$";
			
			Edit_rtxb.Rtf = TextColorSet.keyword(Edit_rtxb.Text, SettingDatas.PLPword1, SettingDatas.PLPword2);

			Edit_rtxb.Select(SettingDatas.cursorSel, 0);
		}
		SettingDatas.PLPdo = false;
	}
	private void Edit_rtxb_Highlight(){
		if(SettingDatas.PLPdo){

			float zooms = Edit_rtxb.ZoomFactor;

			Point pos = new Point(0, 0);
			SendMessage(Edit_rtxb.Handle, 0x04DD, 0, ref pos);

			Edit_rtxb.Enabled = false;

			Edit_rtxb.Rtf = TextColorSet.keyword(Edit_rtxb.Text, SettingDatas.PLPword1, SettingDatas.PLPword2);

			Edit_rtxb.Select(SettingDatas.cursorSel, 0);

			Edit_rtxb.Enabled = true;

			SendMessage(Edit_rtxb.Handle, 0x04DE, 0, ref pos);

			Edit_rtxb.Focus();
			Edit_rtxb.ZoomFactor = zooms;
		}
	}
}



/*--------------------------------------------------*/
// システム関係
/*--------------------------------------------------*/
public static class SystemRelation
{
	/*--------------------------------------------------*/
	// 終了動作
	/*--------------------------------------------------*/
	public static void EditForm_Exit(){
		Console.WriteLine("! " + SettingDatas.mainProgramName + " Closed");

		Environment.Exit(0);
	}
	/*--------------------------------------------------*/
	// エラー表示
	/*--------------------------------------------------*/
	public static void ErrorMes(string text, bool exfl){
		Console.WriteLine("! Warning:" + text);
		MessageBox.Show(text,"エラー - " + SettingDatas.mainProgramName,
		MessageBoxButtons.OK, MessageBoxIcon.Error);
		if(exfl){
			EditForm_Exit();
		}
	}
	/*--------------------------------------------------*/
	// 文字コード判別&文字列読み出し
	/*--------------------------------------------------*/
	public static string getFileData(string path){
		byte[] bs = File.ReadAllBytes(path);
		Encoding enc = DetectEncodingFromBOM(bs);

		if (enc != null){
			int bomLen = enc.GetPreamble().Length;
			SettingDatas.openFileEnc = enc;
			return enc.GetString(bs, bomLen, bs.Length - bomLen);
		}
		enc = GetCode(bs);
		if (enc != null){
			SettingDatas.openFileEnc = enc;
			return enc.GetString(bs);
		}
		SettingDatas.openFileEncStr = "unknown";
		return "[Error]申し訳ございませんこのエディターでは読み込めない形式のファイルです。";
	}
	/*--------------------------------------------------*/
	// 文字列書き出し
	/*--------------------------------------------------*/
	public static void saveFileData(string path, string data){
		File.WriteAllText(path, data, SettingDatas.openFileEnc);
	}
	/*--------------------------------------------------*/
	// 文字コード判別(BOM判定)
	/*--------------------------------------------------*/
	public static Encoding DetectEncodingFromBOM(byte[] bytes){
		if(bytes.Length < 2){
			return null;
		}
		if ((bytes[0] == 0xfe) && (bytes[1] == 0xff)){
			//UTF-16 BE
			SettingDatas.openFileEncStr = "UTF-16 BE(BOM付き)";
			return new UnicodeEncoding(true, true);
		}
		if ((bytes[0] == 0xff) && (bytes[1] == 0xfe)){
			if ((4 <= bytes.Length) &&
				(bytes[2] == 0x00) && (bytes[3] == 0x00)){
				//UTF-32 LE
				SettingDatas.openFileEncStr = "UTF-32 LE(BOM付き)";
				return new UTF32Encoding(false, true);
			}
			//UTF-16 LE
			SettingDatas.openFileEncStr = "UTF-16 LE(BOM付き)";
			return new UnicodeEncoding(false, true);
		}
		if (bytes.Length < 3){
			return null;
		}
		if ((bytes[0] == 0xef) && (bytes[1] == 0xbb) && (bytes[2] == 0xbf)){
			//UTF-8
			SettingDatas.openFileEncStr = "UTF-8(BOM付き)";
			return new UTF8Encoding(true, true);
		}
		if (bytes.Length < 4){
			return null;
		}
		if ((bytes[0] == 0x00) && (bytes[1] == 0x00) &&
			(bytes[2] == 0xfe) && (bytes[3] == 0xff)){
			//UTF-32 BE
			SettingDatas.openFileEncStr = "UTF-32(BOM付き)";
			return new UTF32Encoding(true, true);
		}

		return null;
	}
	/*--------------------------------------------------*/
	// 文字コード判別(Jcode.pm移植)
	/*--------------------------------------------------*/
	public static Encoding GetCode(byte[] bytes){
		const byte bEscape = 0x1B;
		const byte bAt = 0x40;
		const byte bDollar = 0x24;
		const byte bAnd = 0x26;
		const byte bOpen = 0x28;	//'('
		const byte bB = 0x42;
		const byte bD = 0x44;
		const byte bJ = 0x4A;
		const byte bI = 0x49;

		int len = bytes.Length;
		byte b1, b2, b3, b4;

		//Encode::is_utf8 は無視

		bool isBinary = false;
		for (int i = 0; i < len; i++){
			b1 = bytes[i];
			if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF){
				//'binary'
				isBinary = true;
				if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F){
					//smells like raw unicode
					SettingDatas.openFileEncStr = "Unicode";
					return Encoding.Unicode;
				}
			}
		}
		if (isBinary){
			return null;
		}

		//not Japanese
		bool notJapanese = true;
		for (int i = 0; i < len; i++){
			b1 = bytes[i];
			if (b1 == bEscape || 0x80 <= b1){
				notJapanese = false;
				break;
			}
		}
		if (notJapanese){
			SettingDatas.openFileEncStr = "ASCII";
			return Encoding.ASCII;
		}

		for (int i = 0; i < len - 2; i++){
			b1 = bytes[i];
			b2 = bytes[i + 1];
			b3 = bytes[i + 2];

			if (b1 == bEscape){
				if (b2 == bDollar && b3 == bAt){
					//JIS_0208 1978
					//JIS
					SettingDatas.openFileEncStr = "JIS_0208 1978";
					return Encoding.GetEncoding(50220);
				}
				else if (b2 == bDollar && b3 == bB){
					//JIS_0208 1983
					//JIS
					SettingDatas.openFileEncStr = "JIS_0208 1983";
					return Encoding.GetEncoding(50220);
				}
				else if (b2 == bOpen && (b3 == bB || b3 == bJ)){
					//JIS_ASC
					//JIS
					SettingDatas.openFileEncStr = "JIS_ASC";
					return Encoding.GetEncoding(50220);
				}
				else if (b2 == bOpen && b3 == bI){
					//JIS_KANA
					//JIS
					SettingDatas.openFileEncStr = "JIS_KANA";
					return Encoding.GetEncoding(50220);
				}
				if (i < len - 3){
					b4 = bytes[i + 3];
					if (b2 == bDollar && b3 == bOpen && b4 == bD){
						//JIS_0212
						//JIS
						SettingDatas.openFileEncStr = "JIS_0212";
						return Encoding.GetEncoding(50220);
					}
					if (i < len - 5 &&
						b2 == bAnd && b3 == bAt && b4 == bEscape &&
						bytes[i + 4] == bDollar && bytes[i + 5] == bB){
						//JIS_0208 1990
						//JIS
						SettingDatas.openFileEncStr = "JIS_0208 1990";
						return Encoding.GetEncoding(50220);
					}
				}
			}
		}

		//should be euc|sjis|utf8
		//use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
		int sjis = 0;
		int euc = 0;
		int utf8 = 0;
		for (int i = 0; i < len - 1; i++){
			b1 = bytes[i];
			b2 = bytes[i + 1];
			if (((0x81 <= b1 && b1 <= 0x9F) || (0xE0 <= b1 && b1 <= 0xFC)) &&
				((0x40 <= b2 && b2 <= 0x7E) || (0x80 <= b2 && b2 <= 0xFC))){
				//SJIS_C
				sjis += 2;
				i++;
			}
		}
		for (int i = 0; i < len - 1; i++){
			b1 = bytes[i];
			b2 = bytes[i + 1];
			if (((0xA1 <= b1 && b1 <= 0xFE) && (0xA1 <= b2 && b2 <= 0xFE)) ||
				(b1 == 0x8E && (0xA1 <= b2 && b2 <= 0xDF))){
				//EUC_C
				//EUC_KANA
				euc += 2;
				i++;
			}
			else if (i < len - 2){
				b3 = bytes[i + 2];
				if (b1 == 0x8F && (0xA1 <= b2 && b2 <= 0xFE) &&
					(0xA1 <= b3 && b3 <= 0xFE)){
					//EUC_0212
					euc += 3;
					i += 2;
				}
			}
		}
		for (int i = 0; i < len - 1; i++){
			b1 = bytes[i];
			b2 = bytes[i + 1];
			if ((0xC0 <= b1 && b1 <= 0xDF) && (0x80 <= b2 && b2 <= 0xBF)){
				//UTF8
				utf8 += 2;
				i++;
			}
			else if (i < len - 2)
			{
				b3 = bytes[i + 2];
				if ((0xE0 <= b1 && b1 <= 0xEF) && (0x80 <= b2 && b2 <= 0xBF) &&
					(0x80 <= b3 && b3 <= 0xBF)){
					//UTF8
					utf8 += 3;
					i += 2;
				}
			}
		}
		//M. Takahashi's suggestion
		//utf8 += utf8 / 2;

		System.Diagnostics.Debug.WriteLine(
			string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
		if (euc > sjis && euc > utf8){
			//EUC
			SettingDatas.openFileEncStr = "EUC";
			return Encoding.GetEncoding(51932);
		}
		else if (sjis > euc && sjis > utf8){
			//SJIS
			SettingDatas.openFileEncStr = "SJIS";
			return Encoding.GetEncoding(932);
		}
		else if (utf8 > euc && utf8 > sjis){
			//UTF8
			SettingDatas.openFileEncStr = "UTF-8";
			return Encoding.UTF8;
		}

		return null;
	}
}

/*--------------------------------------------------*/
//設定ファイル読み書き
/*--------------------------------------------------*/
public static class SettingFiles
{
	public static void Update(string path, string[] data){
		if(File.Exists(path)){
			using (StreamWriter dest = new StreamWriter(path,false))
			{
				for(int i=0;i<data.Length;i++){
					dest.WriteLine(data[i]);
				}
			}
		}
		else{
			SystemRelation.ErrorMes("\"" + path + "\" does not exist", true);
		}
	}
	public static string[] Reading(string path, int type){
		if(File.Exists(path)){
			Console.WriteLine("@ \"" + path + "\" Load");
			List<string> listSd = new List<string>();
			string line;
			int wi = 0;
			StreamReader src = new StreamReader(path, Encoding.GetEncoding("UTF-8"));
			while(src.EndOfStream == false){
				line = src.ReadLine();
				listSd.Add(line);
				wi++;
			}
			src.Close();
			Console.WriteLine("@ \"" + path + "\" Close");

			return listSd.ToArray();
		}
		else if(Directory.Exists(path)){
			Console.WriteLine("@ \"" + path + "\" Files Load");
			return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
		}
		else{
			if(type == 0){
				SystemRelation.ErrorMes("\"" + path + "\" does not exist", true);
			}
			else if(type == 1){
				SystemRelation.ErrorMes("\"" + path + "\" does not exist", false);
			}
			return null;
		}
	}
}

/*--------------------------------------------------*/
//ソースコードハイライト
/*--------------------------------------------------*/
public static class TextColorSet{
	public static string keyword(string text, List<string> keyword1, List<string> keyword2){
		string[] quotation = SettingDatas.PLPcommand[0].Split('\t');
		string dubc = quotation[0];
		string sinc = "jasc as an not much";
		if(quotation.Length > 1){
			sinc = quotation[1];
		}
		string tancm = SettingDatas.PLPcommand[1];
		string[] fuccm = SettingDatas.PLPcommand[2].Split('\t');
		


		text = text.Replace("\\", "\\\\");

		text = "\\cf1 " + text;
		text = text.Replace("\n", "\a\n\a");
		text = text.Replace("\r", "\a\r\a");
		text = text.Replace("\t", "\a\t\a");
		text = text.Replace("+", "\a+\a");
		text = text.Replace("-", "\a-\a");
		text = text.Replace("*", "\a*\a");
		text = text.Replace("/", "\a/\a");
		text = text.Replace("%", "\a%\a");
		text = text.Replace("=", "\a=\a");
		text = text.Replace("(", "\a(\a");
		text = text.Replace(")", "\a)\a");
		text = text.Replace("[", "\a[\a");
		text = text.Replace("]", "\a]\a");
		text = text.Replace("{", "\a{\a");
		text = text.Replace("}", "\a}\a");
		text = text.Replace("<", "\a<\a");
		text = text.Replace(">", "\a>\a");
		text = text.Replace(".", "\a.\a");
		text = text.Replace(",", "\a,\a");
		text = text.Replace(":", "\a:\a");
		text = text.Replace(";", "\a;\a");
		text = text.Replace(" ", "\a");

		for(int i=0; i<keyword1.Count; i++){
			text = text.Replace("\a"+keyword1[i]+"\a", "\a\\cf5 "+keyword1[i]+"\\cf1 \a");
		}
		for(int i=0; i<keyword2.Count; i++){
			text = text.Replace("\a"+keyword2[i]+"\a", "\a\\cf6 "+keyword2[i]+"\\cf1 \a");
		}

		text = text.Replace("\a\n\a", "\n");
		text = text.Replace("\a\r\a", "\r");
		text = text.Replace("\a\t\a", "\t");
		text = text.Replace("\a+\a", "+");
		text = text.Replace("\a-\a", "-");
		text = text.Replace("\a*\a", "*");
		text = text.Replace("\a/\a", "/");
		text = text.Replace("\a%\a", "%");
		text = text.Replace("\a=\a", "=");
		text = text.Replace("\a(\a", "(");
		text = text.Replace("\a)\a", ")");
		text = text.Replace("\a[\a", "[");
		text = text.Replace("\a]\a", "]");
		text = text.Replace("\a{\a", "{");
		text = text.Replace("\a}\a", "}");
		text = text.Replace("\a<\a", "<");
		text = text.Replace("\a>\a", ">");
		text = text.Replace("\a.\a", ".");
		text = text.Replace("\a,\a", ",");
		text = text.Replace("\a:\a", ":");
		text = text.Replace("\a;\a", ";");
		text = text.Replace("\a", " ");

		text = text.Replace("{", "\\{");
		text = text.Replace("}", "\\}");


		//文字リテラル処理
		if(dubc != "#$notdata#$"){
			try{
				int pos0 = 0;
				string result = "";
				string text3 = text;
				text = text.Replace("\\\\\\\\", "\a\a\a");
				text = text.Replace("\\\"", "\a\a");
				text = text.Replace("\\\\cf", "\acf");
				while(true){
					int pos1=-1;
					int pos2=-1; 
					int pos1a = text.Substring(pos0).IndexOf(dubc);
					int pos2a = text.Substring(pos0+pos1a+1).IndexOf(dubc)+1;
					int pos1b = text.Substring(pos0).IndexOf(sinc);
					int pos2b = text.Substring(pos0+pos1b+1).IndexOf(sinc)+1;
					if(pos1a>=0 && pos2a>=0 && pos1b<0){
						pos1 = pos1a;
						pos2 = pos2a;
					}
					if(pos1a>=0 && pos2a>=0 && pos1a<pos1b){
						pos1 = pos1a; pos2 = pos2a;
					}
					if(pos1b>=0 && pos2b>=0 && pos1a<0){
						pos1 = pos1b;
						pos2 = pos2b;
					}
					if(pos1b>=0 && pos2b>=0 && pos1a>pos1b){
						pos1 = pos1b;
						pos2 = pos2b;
					}
					if(pos1<0 || pos2<0) break;
					int posed = text.Substring(pos0+pos1).IndexOf("\n");
					if(pos2>posed){
						pos2=0;
					}
					string text0 = text.Substring(pos0, pos1);
					string text1 = text.Substring(pos0+pos1, pos2+1);
					text3 = text.Substring(pos0+pos1+pos2+1);
					text1 = text1.Replace(tancm, "\\ec1\\ec").Replace(fuccm[0], "\\ec2\\ec").Replace(fuccm[1], "\\ec3\\ec");
					text1 = text1.Replace("\\cf1 ", "").Replace("\\cf5 ", "").Replace("\\cf6 ", "");
					text1 = "\\cf3 "+ text1 +"\\cf1 ";
					result = result + text0 + text1;
					pos0 = pos0 + pos1 + pos2 + 1;
				}
				text = result + text3;
				text = text.Replace("\a\a\a", "\\\\\\\\");
				text = text.Replace("\a\a", "\\\"");
				text = text.Replace("'\a'", "\"");
			}catch{}
		}

		//コメント処理(単行)
		if(tancm != "#$notdata#$"){
			try{
				int pos0 = 0;
				string result = "";
				string text3 = text;
				while(true){
					int pos1 = text.Substring(pos0).IndexOf(tancm);
					int pos2 = text.Substring(pos0+pos1+1).IndexOf("\n");
					if(pos1<0 || pos2<0) break;
					string text0 = text.Substring(pos0, pos1);
					string text1 = text.Substring(pos0+pos1, pos2+1);
					text3 = text.Substring(pos0+pos1+pos2+1);
					text1 = text1.Replace("\\cf1 ", "").Replace("\\cf3 ", "").Replace("\\cf5 ", "").Replace("\\cf6 ", "");
					text1 = "\\cf4 "+ text1 +"\\cf1 ";
					result = result + text0 + text1;

					pos0 = pos0 + pos1 + pos2 + 1;
				}
				text = result + text3;
			}catch{}
		}


		//コメント処理(複行)
		if(fuccm[0] != "#$notdata#$"){
			try{
				int pos0 = 0;
				string result = "";
				string text3 = text;
				while(true){
					int pos1 = text.Substring(pos0).IndexOf(fuccm[0]);
					int pos2 = text.Substring(pos0+pos1+1).IndexOf(fuccm[1]) + fuccm[1].Length;
					if(pos1<0 || pos2<0) break;
					string text0 = text.Substring(pos0, pos1);
					string text1 = text.Substring(pos0+pos1, pos2+1);
					text3 = text.Substring(pos0+pos1+pos2+1);
					text1 = text1.Replace("\\cf1 ", "").Replace("\\cf3 ", "").Replace("\\cf5 ", "").Replace("\\cf6 ", "");
					text1 = "\\cf4 "+ text1 +"\\cf1 ";
					result = result + text0 + text1;

					pos0 = pos0 + pos1 + pos2 + 1;
				}
				text = result + text3;
			}catch{}
		}

		text = text.Replace("\\ec1\\ec", tancm).Replace("\\ec2\\ec", fuccm[0]).Replace("\\ec3\\ec", fuccm[1]);
		text = text.Replace("\acf", "\\\\cf");
		text = text.Replace("\a", "");
		text = TextColorSet.header() + text + "\n}";
		text = text.Replace("\n", "\\par\n");
		return text;
	}

	private static string header(){
		string header="";
		header +=@"{\rtf1\ansi\deff0\deflang1033\deflangfe1041";
		header +=@"{\fonttbl{\f0\fnil\fcharset128 \'82\'6c\'82\'72 \'83\'53\'83\'56\'83\'62\'83\'4e;}}"; //ＭＳ ゴシック

		header +=@"{\colortbl ;" + SettingDatas.PLPcolor +@"}";

		header +=@"\viewkind4\uc1\pard\li75";//\tx345\tx690\tx1020\tx1365\tx1710\tx2055\tx2385\tx2730\tx3075\tx3420\tx3750\tx4095\tx4440\tx4785\tx5115\tx5460\tx5805\tx6150\tx6480\tx6825\tx7170\tx7515\tx7845\tx8190\tx8535\tx8880\tx9210\tx9555\tx9900\tx10245\tx10575\tx10920";

		header +=@"\cf1"	//\cf1が初期状態のフォント色 1番
			+@"\highlight2"	//初期状態のハイライト色 2番
			+@"\lang1039"	//日本語
			+@"\f0"		//初期のフォントを0番
			+@"\fs24 ";	//フォントサイズ: fs24=12pt
		return header;
	}
}

/*--------------------------------------------------*/
//プラグイン読み込み
/*--------------------------------------------------*/
namespace PluginRelation
{
	/*--------------------------------------------------*/
	//メゾット情報保持
	/*--------------------------------------------------*/
	public class PlugInMethod
	{
		public Type ExeType { get; set; }
		public IList<String> MethodNames { get; set; }
		public PlugInMethod( Type type, IList<String> methods )
		{
			this.ExeType = type;
			this.MethodNames = methods;
		}
	}
	/*--------------------------------------------------*/
	//実行クラス
	/*--------------------------------------------------*/
	public class DoPlugin
	{
		public static void ReadPlugin(){
			var myAsm = Assembly.GetEntryAssembly();
			var exePath = new Uri(myAsm.Location).AbsolutePath;
			var exeDir = HttpUtility.UrlDecode(Path.GetDirectoryName(exePath));
			var methods = GetExecuteMethods(
				exeDir +@"\plugin", "dll",
				SearchOption.TopDirectoryOnly);
			// プラグインのメソッド呼び出し
			foreach( var method in methods )
			{
				CallMethod( method );
			}
		}
		//実行メソッド一覧取得
		public static IList<PlugInMethod> GetExecuteMethods(string dir, string extension, SearchOption seachOption ){
			var methods = new SortedList<Int32,PlugInMethod>();
			string[] dllPaths = Directory.GetFiles(dir,"*."+extension,seachOption);
			//対象dll検索
			foreach( var dllPath in dllPaths )
			{
				var asmDll = GetPlugInAssembly(dllPath);
				if( asmDll != null )
				{
					Console.WriteLine("$ " + Path.GetFileName(dllPath) + " Load");
					//対象クラス検索
					var classTypes = asmDll.GetTypes();
					foreach( var classType in classTypes )
					{
						var typeAttrs = classType.GetCustomAttributes(typeof(PlugInClassAttribute));
						if( typeAttrs == null || typeAttrs.Count() == 0 )
						{
							continue;
						}
						var names = new SortedList<Int32,String>();
						var classIndex = 0;
						foreach( var typeAttr in typeAttrs )
						{
							var plugInClass = typeAttr as PlugInClassAttribute;
							if( plugInClass != null )
							{
								classIndex = plugInClass.ClassIndex;
								names.Add( plugInClass.MethodIndex,
											plugInClass.ExecuteName );
							}
						}
						//メソッド名を保持
						methods.Add(
							classIndex,
							new PlugInMethod(
								classType,
								names.Values ) );
					}
				}
			}
			return methods.Values;
		}
		//PlugIn取得
		public static Assembly GetPlugInAssembly( String dllPath )
		{
			var asmDll = Assembly.LoadFile(dllPath);
			if( asmDll != null )
			{
				var asmAttr = asmDll.GetCustomAttribute(
							 typeof(PlugInAssemblyAttribute));
				if( asmAttr != null )
				{
					var asmPlugIn = asmAttr as PlugInAssemblyAttribute;
					if( asmPlugIn != null )
					{
						return asmDll;
					}
				}
			}
			return null;
		}
		//メソッド呼び出し
		public static void CallMethod( PlugInMethod method )
		{
			var classType = method.ExeType;
			var pluginInstance = Activator.CreateInstance(classType);
			foreach( var methodName in method.MethodNames )
			{
				Console.WriteLine("$$ " + methodName + " Called");
				MethodInfo methodInfo = classType.GetMethod(methodName);
				methodInfo.Invoke( pluginInstance, null );
			}
		}
	}
}

/*--------------------------------------------------*/
//正規表現 replace
/*--------------------------------------------------*/
public static class RegexExtension
{
	public static string RegexReplace(this string input, string pattern, string replacement){
		return Regex.Replace(input, pattern, replacement);
	}
}

/*--------------------------------------------------*/
//RichテキストボックスIME対応
/*--------------------------------------------------*/
public class RichTextBoxJapanese : RichTextBox
{

	[DllImport("imm32.dll", CharSet = CharSet.Unicode)]
	private static extern int ImmGetCompositionString(IntPtr hIMC, uint dwIndex, byte[] lpBuf, int dwBufLen);

	[DllImport("imm32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr ImmGetContext(IntPtr hWnd);

	[DllImport("imm32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr ImmReleaseContext(IntPtr hWnd, IntPtr context);


	public enum WM_IME
	{
		GCS_RESULTSTR = 0x800,
		EM_STREAMOUT = 0x044A,
		WM_IME_COMPOSITION =0x10F,
		WM_IME_ENDCOMPOSITION =0x10E, 	
		WM_IME_STARTCOMPOSITION =0x10D
	}

	private bool skipImeComposition = false;
	private bool imeComposing = false;

	public bool JapaneseWorkaroundEnabled = true;

	string _mText = "";

	protected override void WndProc(ref Message m)
	{
		if (JapaneseWorkaroundEnabled){
			switch (m.Msg){
				case (int)WM_IME.EM_STREAMOUT:
					if (imeComposing){
						skipImeComposition = true;
					}
					base.WndProc(ref m);
					break;

				case (int)WM_IME.WM_IME_COMPOSITION:
					if (m.LParam.ToInt32() == (int)WM_IME.GCS_RESULTSTR){
						IntPtr hImm = ImmGetContext(this.Handle);
						int dwSize = ImmGetCompositionString(hImm, (int)WM_IME.GCS_RESULTSTR, null, 0);
						byte[] outstr = new byte[dwSize];
						ImmGetCompositionString(hImm, (int)WM_IME.GCS_RESULTSTR, outstr, dwSize);
						_mText += Encoding.Unicode.GetString(outstr).ToString();
						ImmReleaseContext(this.Handle, hImm);
					}
					if (skipImeComposition){
						skipImeComposition = false;
						break;
					}
					base.WndProc(ref m);
					break;

				case (int)WM_IME.WM_IME_STARTCOMPOSITION:
					imeComposing = true;
					base.WndProc(ref m);
					break;

				case (int)WM_IME.WM_IME_ENDCOMPOSITION:
					imeComposing = false;
					base.WndProc(ref m);
					break;

				default:
					base.WndProc(ref m);
					break;
			}
		}
		else{
			base.WndProc(ref m);
		}
	}

	public override string Text
	{
		get
		{
			if (!imeComposing){
				_mText = base.Text;
				return base.Text;
			}
			else{
				return _mText;
			}
		}
		set{
			base.Text = value;
			_mText = value;
		}
	}
}