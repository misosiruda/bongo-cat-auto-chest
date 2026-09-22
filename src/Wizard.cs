using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace BongoAutoChest.Setup
{
    static class Program
    {
        public static readonly string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BongoAutoChest");
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var mutex = new Mutex(false,@"Local\BongoAutoChestWizard"))
            {
                bool owned = false;
                try
                {
                    try { owned = mutex.WaitOne(3000); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) { MessageBox.Show("이미 열려 있는 Bongo Cat 도우미 창을 확인해 주세요.","Bongo Cat 도우미"); return 1; }
                    string root = ExtractPayload();
                    string game = args.Length == 2 && args[0] == "--game" ? args[1] : "";
                    Application.Run(new Wizard(root,game));
                    return 0;
                }
                catch (Exception ex) { MessageBox.Show("도우미를 시작하지 못했어요. 파일을 다시 다운로드한 뒤 실행해 주세요.\n\n" + ex.Message,"Bongo Cat 도우미",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
                finally { if (owned) mutex.ReleaseMutex(); }
            }
        }
        static string ExtractPayload()
        {
            byte[] data;
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
            using (var buffer = new MemoryStream()) { input.CopyTo(buffer); data = buffer.ToArray(); }
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");
            string root = Path.Combine(Home,"runtime",hash.Substring(0,20)); Directory.CreateDirectory(root);
            using (var archive = new ZipArchive(new MemoryStream(data),ZipArchiveMode.Read))
                foreach (var entry in archive.Entries)
                {
                    string path = Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
                    if (!path.StartsWith(root + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid bundled path.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open()) using (var output = File.Create(path)) input.CopyTo(output);
                }
            return root;
        }
    }
    sealed class Wizard : Form
    {
        readonly Color Ink = Color.FromArgb(31,39,57), Muted = Color.FromArgb(102,112,133), Accent = Color.FromArgb(80,73,209);
        readonly Panel body = new Panel(), footer = new Panel();
        readonly Label steps = new Label(), title = new Label(), subtitle = new Label();
        readonly Button next = new Button(), back = new Button();
        readonly string root;
        string game, loadedGame = "", detail = "", operation = "Launch";
        ChestSettings settings = new ChestSettings();
        bool busy, existing, shortcut = true;
        int page;
        CheckBox enabled, own, others, shortcutBox;
        NumericUpDown minimum, maximum;
        Label validation;
        public Wizard(string root, string game)
        {
            this.root = root; this.game = game;
            Text = "Bongo Cat 도우미"; Font = new Font("맑은 고딕",10F); ForeColor = Ink;
            BackColor = Color.White; ClientSize = new Size(820,650); MinimumSize = new Size(760,620);
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            TopMost = true; // Keep the settings usable over Bongo Cat's desktop overlay.
            AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
            Icon = SystemIcons.Application;
            var header = new Panel { Dock = DockStyle.Top, Height = 140, BackColor = Color.FromArgb(247,247,253) };
            steps.SetBounds(34,19,740,24); steps.ForeColor = Accent; steps.Font = new Font(Font.FontFamily,9F,FontStyle.Bold);
            title.SetBounds(34,51,740,38); title.Font = new Font(Font.FontFamily,20F,FontStyle.Bold);
            subtitle.SetBounds(36,98,740,30); subtitle.ForeColor = Muted;
            header.Controls.AddRange(new Control[] { steps,title,subtitle });
            footer.Dock = DockStyle.Bottom; footer.Size = new Size(ClientSize.Width,78); footer.BackColor = Color.FromArgb(247,247,250);
            back.SetBounds(34,20,115,38); back.Text = "이전"; StyleButton(back,false); back.Click += delegate { if (page == 1) { settings = Selected(); shortcut = shortcutBox.Checked; ShowGame(); } else ShowSettings(); };
            next.SetBounds(567,20,218,38); next.Anchor = AnchorStyles.Right | AnchorStyles.Top; StyleButton(next,true);
            next.Click += delegate { if (page == 0) ShowSettings(); else if (page == 1) Apply(); else Close(); };
            footer.Controls.AddRange(new Control[] { back,next });
            body.Dock = DockStyle.Fill; body.AutoScroll = true;
            Controls.Add(body); Controls.Add(header); Controls.Add(footer);
            FormClosing += delegate(object sender,FormClosingEventArgs e) {
                if (busy) { e.Cancel = true; MessageBox.Show(this,"적용을 마칠 때까지 잠시 기다려 주세요.",Text); }
            };
            Shown += delegate { Discover(); };
        }
        void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(211,214,223);
            button.BackColor = primary ? Accent : Color.White; button.ForeColor = primary ? Color.White : Ink;
            button.Cursor = Cursors.Hand; button.UseVisualStyleBackColor = false;
        }
        Label LabelAt(string text,int x,int y,int width,int height,bool bold)
        {
            var label = new Label { Text = text, AutoSize = false, ForeColor = bold ? Ink : Muted };
            label.SetBounds(x,y,width,height); if (bold) label.Font = new Font(Font,FontStyle.Bold);
            body.Controls.Add(label); return label;
        }
        Button ButtonAt(string text,int x,int y,int width,Action action)
        {
            var b = new Button { Text = text }; b.SetBounds(x,y,width,37); StyleButton(b,false);
            b.Click += delegate { action(); }; body.Controls.Add(b); return b;
        }
        void Reset(int p,string heading,string sub)
        {
            page = p;
            foreach (Control control in body.Controls.Cast<Control>().ToArray()) control.Dispose();
            body.Controls.Clear(); body.AutoScrollPosition = Point.Empty;
            steps.Text = p < 2 ? "01  게임 찾기     /     02  개봉 설정     /     03  완료" : busy ? "03  적용 중" : p == 3 ? "설정 확인" : "03  완료";
            title.Text = heading; subtitle.Text = sub; next.Enabled = true; next.Visible = true; back.Visible = p == 1;
        }
        void Discover()
        {
            Reset(0,"상자는 맡기고, 편하게 즐겨요","Bongo Cat이 설치된 위치를 자동으로 찾고 있어요."); next.Enabled = false;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object s,DoWorkEventArgs e) { e.Result = GameLocation.IsGame(game) ? game : GameLocation.Discover(); };
            worker.RunWorkerCompleted += delegate(object s,RunWorkerCompletedEventArgs e) {
                if (IsDisposed) return;
                game = e.Error == null ? (string)e.Result : ""; ShowGame(); worker.Dispose();
            };
            worker.RunWorkerAsync();
        }
        void ShowGame()
        {
            Reset(0,"상자는 맡기고, 편하게 즐겨요","몇 가지 선택만 하면 자동 개봉을 시작할 수 있어요.");
            bool found = GameLocation.IsGame(game);
            LabelAt(found ? "Bongo Cat을 찾았어요" : "게임 폴더를 선택해 주세요",36,28,740,30,true).ForeColor = found ? Color.FromArgb(25,122,92) : Ink;
            var path = new TextBox { ReadOnly = true, Text = found ? game : "Steam에서 Bongo Cat을 먼저 설치해 주세요.", BorderStyle = BorderStyle.FixedSingle };
            path.SetBounds(36,71,748,32); path.AccessibleName = "게임 설치 위치"; body.Controls.Add(path);
            ButtonAt("폴더 변경…",36,118,135,delegate {
                using (var dialog = new FolderBrowserDialog { Description = "BongoCat.exe가 들어 있는 폴더를 선택하세요.", ShowNewFolderButton = false })
                    if (dialog.ShowDialog(this) == DialogResult.OK) {
                        if (!GameLocation.IsGame(dialog.SelectedPath)) { MessageBox.Show(this,"선택한 폴더에서 Bongo Cat을 찾지 못했어요. BongoCat.exe가 있는 폴더를 선택해 주세요.",Text); return; }
                        game = dialog.SelectedPath; ShowGame();
                    }
            });
            LabelAt("처음에는 내 상자만 자동으로 열어요",36,192,740,28,true);
            LabelAt("일반 상자와 감정표현 상자를 처리해요.\n다른 사람의 상자 개봉은 다음 화면에서 선택할 수 있어요.",36,232,740,62,false);
            LabelAt("시작 전 Steam 업데이트를 마쳐 주세요. 적용이 필요하면 게임을 정상 종료한 뒤 다시 실행해요.",36,322,740,52,false);
            next.Text = "개봉 설정으로 →"; next.Enabled = found;
            if (found) {
                var restore = ButtonAt("자동 개봉 제거 / 원본 복원",36,380,270,delegate {
                    if (MessageBox.Show(this,"자동 개봉을 제거하고 현재 버전의 원본으로 복원할까요?\n게임이 실행 중이면 종료합니다. 개봉 설정은 보관합니다.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question) == DialogResult.Yes) RunOperation("Restore");
                });
                restore.Enabled = true;
            }
        }
        CheckBox CheckAt(string text,int x,int y,int width,bool value)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = false, Cursor = Cursors.Hand };
            c.SetBounds(x,y,width,30); body.Controls.Add(c); return c;
        }
        void ShowSettings()
        {
            try {
                if (!string.Equals(loadedGame,game,StringComparison.OrdinalIgnoreCase)) {
                    existing = File.Exists(Path.Combine(game,"BongoAutoChest.ini")); settings = ChestSettings.Read(Path.Combine(game,"BongoAutoChest.ini")); loadedGame = game;
                }
            }
            catch (Exception ex) { ShowError("설정 파일을 읽지 못했어요.",ex.ToString()); return; }
            RenderSettings();
        }
        void RenderSettings()
        {
            Reset(1,"어떤 상자를 열까요?",existing ? "저장된 설정을 불러왔어요. 원하는 항목만 선택하세요." : "기본값은 내 상자만 개봉해요. 나중에 언제든 바꿀 수 있어요.");
            enabled = CheckAt("자동 개봉 사용",36,16,700,settings.Enabled); enabled.Font = new Font(Font,FontStyle.Bold);
            own = CheckAt("내 상자 자동 개봉",36,64,700,settings.AutoOwn); own.Font = new Font(Font,FontStyle.Bold);
            LabelAt("내 일반 상자와 감정표현 상자가 준비되면 열어요.",58,97,704,25,false);
            others = CheckAt("다른 사람의 상자도 개봉",36,135,700,settings.AutoOthers); others.Font = new Font(Font,FontStyle.Bold);
            var cost = LabelAt("선택 사항 · 로비에 보이는 상자를 열 때 내 포인트를 소비해요.",58,168,704,28,false);
            cost.ForeColor = Color.FromArgb(158,92,21);
            LabelAt("발견·개봉 대기 시간",36,219,240,26,true);
            minimum = new NumericUpDown { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Value = settings.MinDelaySeconds, AccessibleName = "최소 대기 시간" };
            maximum = new NumericUpDown { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Value = settings.MaxDelaySeconds, AccessibleName = "최대 대기 시간" };
            minimum.SetBounds(270,217,85,30); maximum.SetBounds(405,217,85,30); body.Controls.AddRange(new Control[] { minimum,maximum });
            LabelAt("~",373,222,25,26,false); LabelAt("초",503,222,30,26,false);
            ButtonAt("기본값 3~8초",580,213,178,delegate { minimum.Value = 3; maximum.Value = 8; });
            shortcutBox = CheckAt("바탕화면에 도우미 바로 가기 만들기",36,278,720,shortcut);
            LabelAt("상자 발견 후와 개봉 사이에 같은 범위를 적용해요.\n게임 중에는 Ctrl+Alt+F9로 켜고 끌 수 있어요.",36,322,748,48,false);
            validation = LabelAt("",36,378,748,45,false); validation.ForeColor = Color.FromArgb(170,51,51);
            EventHandler update = delegate { UpdateSelection(); };
            enabled.CheckedChanged += update; own.CheckedChanged += update; others.CheckedChanged += update;
            minimum.ValueChanged += update; maximum.ValueChanged += update;
            next.Text = "설정 저장하고 게임 실행"; UpdateSelection();
        }
        ChestSettings Selected()
        {
            return new ChestSettings { Enabled = enabled.Checked, AutoOwn = own.Checked, AutoOthers = others.Checked,
                MinDelaySeconds = minimum.Value, MaxDelaySeconds = maximum.Value };
        }
        void UpdateSelection()
        {
            settings = Selected(); shortcut = shortcutBox.Checked;
            string message = settings.Validate(); validation.Text = message ?? ""; next.Enabled = message == null;
            own.Enabled = others.Enabled = minimum.Enabled = maximum.Enabled = enabled.Checked;
            next.Text = enabled.Checked ? "설정 저장하고 게임 실행" : "자동 개봉 끄고 게임 실행";
        }
        void Apply()
        {
            settings = Selected(); shortcut = shortcutBox.Checked;
            if (settings.Validate() != null) { UpdateSelection(); return; }
            RunOperation("Launch");
        }
        void RunOperation(string action)
        {
            operation = action; busy = true;
            Reset(2,action == "Restore" ? "원본으로 복원하고 있어요" : "설정을 적용하고 있어요","완료될 때까지 이 창을 열어 두세요.");
            next.Visible = false; back.Visible = false;
            var progress = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            progress.SetBounds(36,71,748,14); body.Controls.Add(progress);
            LabelAt(action == "Restore" ? "현재 게임에 맞는 원본을 확인하고 복원해요." : "현재 게임 버전을 확인한 뒤 자동 개봉 설정을 저장해요.",36,124,740,60,true);
            LabelAt("게임 종료가 필요하면 잠시 기다릴 수 있어요.\n검사가 끝나기 전에 창을 닫지 말아 주세요.",36,207,740,66,false);
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender,DoWorkEventArgs e) { e.Result = Backend(action); };
            worker.RunWorkerCompleted += delegate(object sender,RunWorkerCompletedEventArgs e) {
                busy = false; worker.Dispose(); if (IsDisposed) return;
                if (e.Error != null) { ShowError("작업을 마치지 못했어요.",e.Error.ToString()); return; }
                var result = (BackendResult)e.Result;
                if (result.ExitCode != 0) { ShowError(FriendlyError(result.Log),result.Log); return; }
                string shortcutWarning = "";
                if (action == "Launch" && shortcut) {
                    try { CreateShortcut(); } catch (Exception ex) { shortcutWarning = "바로 가기를 만들지 못했어요. 다운로드한 실행 파일은 계속 사용할 수 있어요.\n" + ex.Message; }
                }
                ShowDone(shortcutWarning);
            };
            worker.RunWorkerAsync();
        }
        sealed class BackendResult { public int ExitCode; public string Log; }
        BackendResult Backend(string action)
        {
            string request = Path.Combine(root,"settings-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + GameLocation.Quote(Path.Combine(root,"Launcher.ps1"))
                    + " -Action " + action + " -GameDirectory " + GameLocation.Quote(game);
                if (action == "Launch") { File.WriteAllText(request,new JavaScriptSerializer().Serialize(settings),Encoding.UTF8); arguments += " -SettingsPath " + GameLocation.Quote(request); }
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),arguments) {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                var output = new StringBuilder();
                using (var process = new Process { StartInfo = start })
                {
                    DataReceivedEventHandler receive = delegate(object sender,DataReceivedEventArgs e) { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    process.OutputDataReceived += receive; process.ErrorDataReceived += receive;
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                    return new BackendResult { ExitCode = process.ExitCode, Log = output.ToString() };
                }
            }
            finally { if (File.Exists(request)) File.Delete(request); }
        }
        string FriendlyError(string log)
        {
            if (log.Contains("Close Bongo Cat") || log.Contains("Bongo Cat is running")) return "게임을 완전히 종료한 뒤 다시 시도해 주세요.\nBongo Cat의 메뉴 또는 트레이 아이콘에서 종료할 수 있어요.";
            if (log.Contains("denied") || log.Contains("UnauthorizedAccess") || log.Contains("거부")) return "게임 폴더에 설정을 저장할 권한이 없어요.\n아래 버튼으로 관리자 권한으로 다시 열어 주세요.";
            if (log.Contains("backup") || log.Contains("Backup")) return "복원에 필요한 원본을 확인하지 못했어요.\nSteam에서 게임 파일 무결성을 확인한 뒤 다시 시도해 주세요.";
            if (log.Contains("Incompatible") || log.Contains("MISSING") || log.Contains("compile")) return "현재 게임 버전과 호환되지 않아요.\n새 도우미 버전이 있는지 다운로드 페이지를 확인해 주세요.";
            return "작업을 마치지 못했어요. Steam 업데이트와 게임 종료 상태를 확인하고 다시 시도해 주세요.";
        }
        void ShowError(string message,string log)
        {
            detail = log; Reset(3,"잠깐, 확인이 필요해요","아래 안내를 확인하면 다시 진행할 수 있어요.");
            LabelAt(message,36,30,748,100,true);
            ButtonAt("다시 시도",36,155,150,delegate { RunOperation(operation); });
            ButtonAt("설정으로 돌아가기",202,155,205,delegate { if (GameLocation.IsGame(game)) ShowSettings(); else ShowGame(); });
            if (log.Contains("denied") || log.Contains("UnauthorizedAccess") || log.Contains("거부"))
                ButtonAt("관리자 권한으로 다시 열기",36,210,285,delegate {
                    try { Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--game " + GameLocation.Quote(game)) { UseShellExecute = true, Verb = "runas" }); Close(); }
                    catch (Win32Exception) { MessageBox.Show(this,"관리자 권한 실행이 취소되었어요.",Text); }
                });
            ButtonAt("자세한 내용 보기",36,274,200,delegate { ShowDetails(); });
            ButtonAt("다운로드 페이지",252,274,190,delegate { OpenUrl("https://github.com/misosiruda/bongo-cat-auto-chest/releases/latest"); });
            next.Text = "닫기";
        }
        void ShowDetails()
        {
            using (var dialog = new Form { Text = "작업 상세 내용", StartPosition = FormStartPosition.CenterParent, Size = new Size(750,420), Font = Font })
            {
                dialog.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Text = detail, WordWrap = false });
                dialog.ShowDialog(this);
            }
        }
        void ShowDone(string warning)
        {
            bool restored = operation == "Restore";
            Reset(2,restored ? "원본 복원이 끝났어요" : "설정을 저장했어요",restored ? "이제 Steam에서 평소처럼 게임을 실행하세요." : "Steam에 게임 실행을 요청했어요. 게임이 이미 켜져 있다면 그대로 사용할 수 있어요.");
            LabelAt(restored ? "자동 개봉이 제거되었어요" : settings.Enabled ? "자동 개봉 켜짐" : "자동 개봉 꺼짐",36,30,740,36,true).ForeColor = Color.FromArgb(25,122,92);
            if (!restored) {
                LabelAt("내 상자: " + (settings.AutoOwn ? "켜짐" : "꺼짐") + "     ·     다른 사람의 상자: " + (settings.AutoOthers ? "켜짐" : "꺼짐")
                    + "\n발견·개봉 대기: " + settings.MinDelaySeconds + "~" + settings.MaxDelaySeconds + "초",36,90,748,75,true);
                LabelAt("설정을 바꾸려면 도우미를 다시 열어 주세요.\n게임 업데이트 후에도 이 도우미로 실행하면 다시 적용해요.",36,194,748,70,false);
                if (!string.IsNullOrEmpty(warning)) LabelAt(warning,36,290,748,90,false);
                ButtonAt("개봉 설정 바꾸기",36,375,215,delegate { ShowSettings(); });
            } else LabelAt("개봉 설정과 원본 백업은 보관했어요.\n자동 개봉을 다시 사용하려면 도우미에서 설정을 적용하세요.",36,98,748,80,false);
            next.Text = "마침";
        }
        void CreateShortcut()
        {
            Directory.CreateDirectory(Program.Home);
            string executable = Path.Combine(Program.Home,"BongoAutoChest.exe"), current = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(executable,current,StringComparison.OrdinalIgnoreCase)) File.Copy(current,executable,true);
            Type type = Type.GetTypeFromProgID("WScript.Shell"); object shell = Activator.CreateInstance(type), link = null;
            try {
                link = type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Bongo Cat 도우미.lnk") });
                link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[] { executable });
                link.GetType().InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[] { "--game " + GameLocation.Quote(game) });
                link.GetType().InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[] { Program.Home });
                link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
            } finally {
                if (link != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(link);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }
        void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this,ex.Message,Text); } }
    }
}
