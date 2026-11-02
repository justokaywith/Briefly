using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Briefly
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) => LogCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash(e.ExceptionObject as Exception);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }

        private static void LogCrash(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logFile = Path.Combine(appData, "Briefly", "app.log");
                File.AppendAllText(logFile, string.Format("[{0}] Exception: {1}\r\nStack: {2}\r\n\r\n", DateTime.UtcNow.ToString("o"), ex.Message, ex.StackTrace));
            }
            catch { }
        }

        private static void SaveCliPairToken(string token)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "Briefly");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string cfg = Path.Combine(dir, "config.json");
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> dict = new Dictionary<string, object>();
                dict["pairToken"] = token;
                dict["updatedAt"] = DateTime.UtcNow.ToString("o");
                File.WriteAllText(cfg, js.Serialize(dict));
            }
            catch { }
        }
    }

    public partial class MainForm : Form
    {
        // Theme Colors (Apple HIG / Linear minimalist)
        private Color clrBg = Color.FromArgb(248, 249, 251);
        private Color clrSurface = Color.FromArgb(255, 255, 255);
        private Color clrBorder = Color.FromArgb(229, 231, 235);
        private Color clrBorderLight = Color.FromArgb(243, 244, 246);
        private Color clrInk = Color.FromArgb(17, 24, 39);
        private Color clrInkSec = Color.FromArgb(107, 114, 128);
        private Color clrAccent = Color.FromArgb(17, 24, 39);
        private Color clrGreen = Color.FromArgb(16, 185, 129);
        private Color clrRed = Color.FromArgb(239, 68, 68);

        // State
        private string appVersion = "1.3.5";
        private string apiOrigin = "https://briefly.pactuno.online";
        private string pairToken = null;
        private Dictionary<string, object> currentUser = null;
        private bool isAuthenticated = false;

        private bool isRecording = false;
        private DateTime recordingStartTime;
        private int[] waveHeights = new int[26];
        private CaptureSession audioCapture;
        private CheckBox liveListen;
        private readonly LiveListenState listenState = new LiveListenState();
        private int detectorBusy;
        private bool detectorPromptOpen,recordingFromDetection;
        private volatile bool liveListenEnabled;
        private bool restoringListen;
        private int detectorGeneration;
        private ListenPreferences listenPreferences=new ListenPreferences();
        private volatile HashSet<string> ignoredAppKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string lastDetectionLog;
        private MeetingSignal latestMeetingSignal=new MeetingSignal();
        private string capturedMeetingTitle,capturedPlatform,capturedSignalKey;
        private DateTime capturedStartedAt;
        private int syncRetryBusy;
        private CancellationTokenSource authCancellation;
        private string activeMeetingTitle = "Listening for Google Meet, Teams & Zoom...";
        private bool isMeetingDetected = false;

        // UI Controls - Header
        private Panel pnlUpdateBanner;
        private Label lblUpdateMsg;
        private Button btnUpdateLink;
        private Panel pnlHeader;
        private Label lblBrand;
        private Label lblVer;
        private Panel pnlTabs;
        private Control btnTabRecord;
        private Control btnTabCloud;
        private Control btnTabSettings;

        // Header Right Area (proper flow/alignment, no overlap!)
        private Panel pnlHeaderRight;
        private Label lblUserChip;
        private Button btnWebPortal;
        private Button btnSignOut;

        // Body & Views
        private Panel pnlBody;
        private Panel pnlAuthView;
        private Panel pnlAuthCard;
        private Panel pnlViewRecord;
        private Panel pnlViewCloud;
        private Panel pnlViewSettings;

        // Auth Form Controls
        private Label lblAuthTitle;
        private Label lblAuthSubtitle;
        private Label lblAuthAlert;
        private Button btnAuthSubmit;

        // Capture Tab Controls
        private Panel pnlWaveform;
        private Label lblTimer;
        private Button btnRecord;
        private Label lblStatus;
        private Label lblRadarText;
        private PictureBox picRadarDot;
        private TextBox txtLocalNotes;

        // Cloud Tab Controls
        private ListView lvCloudMeetings;
        private Label lblCloudCount;
        private Label lblCloudUser;
        private Label lblCloudDetailTitle;
        private Label lblCloudDetailMeta;
        private TextBox txtCloudDetailSummary;
        private ListView lvCloudDetailActions;
        private TextBox txtCloudTranscript;
        private List<Dictionary<string, object>> cloudMeetingList = new List<Dictionary<string, object>>();

        // Settings Tab Controls
        private Label lblSettingsUser;
        private Label lblSettingsStatus;
        private TextBox txtPairToken;
        private Label lblVersionInfo;

        // Timers
        private System.Windows.Forms.Timer timerClock;
        private System.Windows.Forms.Timer timerWave;
        private System.Windows.Forms.Timer timerDetector;

        private string dataDir;
        private string recordingsDir;

        public MainForm():this(true){}
        internal MainForm(bool enableServices)
        {
            ApplyCustomStyles();
            InitializeDataPaths();
            LoadConfig();
            InitializeComponent();
            ApplyReferenceLayout();

            // Set Form Icon from briefly.ico
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "briefly-"+appVersion+".ico");
            if (File.Exists(icoPath))
            {
                try { this.Icon = new Icon(icoPath); } catch { }
            }

            // Timers
            timerClock = new System.Windows.Forms.Timer { Interval = 1000 };
            timerClock.Tick += TimerClock_Tick;

            timerWave = new System.Windows.Forms.Timer { Interval = 80 };
            timerWave.Tick += TimerWave_Tick;

            timerDetector = new System.Windows.Forms.Timer { Interval = 3000 };
            timerDetector.Tick += (s, e) => { if (isAuthenticated&&liveListenEnabled) ThreadPool.QueueUserWorkItem(state => CheckRunningMeetings()); };
            if(!enableServices){ShowAuthView();return;}
            listenPreferences=ListenPreferences.LoadFrom(ListenPreferences.Store);
            ignoredAppKeys=new HashSet<string>(listenPreferences.IgnoredApps,StringComparer.OrdinalIgnoreCase);
            restoringListen=true;liveListen.Checked=listenPreferences.Enabled;liveListenEnabled=listenPreferences.Enabled;restoringListen=false;
            DesktopDiagnostics.Detection=liveListenEnabled?"Waiting for sign-in / next poll":"Off";
            DesktopDiagnostics.Record("startup","local",liveListenEnabled?"listen-restored-on":"listen-restored-off");
            timerDetector.Start();

            // Startup state
            if (!string.IsNullOrEmpty(pairToken))
            {
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    VerifyCloudConnection();
                    CheckForAppUpdates();
                });
            }
            else
            {
                ShowAuthView();
                ThreadPool.QueueUserWorkItem((state) => {
                    try { pairToken=DesktopAuth.AccessToken();if(!string.IsNullOrEmpty(pairToken))VerifyCloudConnection(); } catch { }
                    CheckForAppUpdates();
                });
            }
        }

        private void InitializeDataPaths()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dataDir = Path.Combine(appData, "Briefly");
            recordingsDir = Path.Combine(dataDir, "recordings");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            if (!Directory.Exists(recordingsDir)) Directory.CreateDirectory(recordingsDir);

            for (int i = 0; i < waveHeights.Length; i++) waveHeights[i] = 6;
        }

        private void LoadConfig() { pairToken=null; }

        private void ClearConfig()
        {
            try
            {
                pairToken = null;
                currentUser = null;
                isAuthenticated = false;
                DesktopAuth.SignOut();
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Briefly — Meeting Intelligence";
            this.ClientSize = new Size(1060, 720);
            this.MinimumSize = new Size(980, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBg;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // 1. Top Update Banner
            pnlUpdateBanner = new Panel();
            pnlUpdateBanner.Dock = DockStyle.Top;
            pnlUpdateBanner.Height = 36;
            pnlUpdateBanner.BackColor = Color.FromArgb(17, 24, 39);
            pnlUpdateBanner.Visible = false;

            Label lblUpdateBadge = new Label();
            lblUpdateBadge.Text = "UPDATE AVAILABLE";
            lblUpdateBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblUpdateBadge.ForeColor = Color.White;
            lblUpdateBadge.BackColor = Color.FromArgb(59, 130, 246);
            lblUpdateBadge.Padding = new Padding(4, 2, 4, 2);
            lblUpdateBadge.AutoSize = true;
            lblUpdateBadge.Location = new Point(16, 8);

            lblUpdateMsg = new Label();
            lblUpdateMsg.Text = "A newer version of Briefly is available on pactuno.online.";
            lblUpdateMsg.ForeColor = Color.White;
            lblUpdateMsg.Font = new Font("Segoe UI", 9F);
            lblUpdateMsg.AutoSize = true;
            lblUpdateMsg.Location = new Point(140, 8);

            btnUpdateLink = new Button();
            btnUpdateLink.Text = "Download Update (.exe)";
            btnUpdateLink.FlatStyle = FlatStyle.Flat;
            btnUpdateLink.FlatAppearance.BorderSize = 0;
            btnUpdateLink.BackColor = Color.White;
            btnUpdateLink.ForeColor = Color.FromArgb(17, 24, 39);
            btnUpdateLink.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnUpdateLink.Size = new Size(160, 24);
            btnUpdateLink.Location = new Point(860, 6);
            btnUpdateLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdateLink.Click += (s, e) => { Process.Start(new ProcessStartInfo("https://briefly.pactuno.online/#download"){UseShellExecute=true}); };

            pnlUpdateBanner.Controls.Add(lblUpdateBadge);
            pnlUpdateBanner.Controls.Add(lblUpdateMsg);
            pnlUpdateBanner.Controls.Add(btnUpdateLink);

            // 2. Main Header
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 56;
            pnlHeader.BackColor = clrSurface;
            pnlHeader.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(clrBorder), 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            // Icon & Brand
            AnimatedLogo picBrand = new AnimatedLogo();
            picBrand.Size = new Size(30, 30);
            picBrand.Location = new Point(18, 13);

            lblBrand = new Label();
            lblBrand.Text = "Briefly";
            lblBrand.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblBrand.ForeColor = clrInk;
            lblBrand.Location = new Point(54, 15);
            lblBrand.AutoSize = true;

            lblVer = new Label();
            lblVer.Text = "v" + appVersion;
            lblVer.Font = new Font("Segoe UI", 8.5F);
            lblVer.ForeColor = clrInkSec;
            lblVer.Location = new Point(116, 20);
            lblVer.AutoSize = true;

            // Nav Tabs (Centered)
            pnlTabs = new Panel();
            pnlTabs.Size = new Size(390, 36);
            pnlTabs.Location = new Point(200, 10);
            pnlTabs.BackColor = Color.FromArgb(243, 244, 246);
            pnlTabs.Padding = new Padding(3);
            pnlTabs.Visible = false; // Hidden until authenticated

            btnTabRecord = CreateTabButton("🎙️ Capture", true);
            btnTabRecord.Location = new Point(3, 3);
            btnTabRecord.Click += (s, e) => SwitchTab("record");

            btnTabCloud = CreateTabButton("☁️ Cloud Workspace", false);
            btnTabCloud.Location = new Point(125, 3);
            btnTabCloud.Click += (s, e) => SwitchTab("cloud");

            btnTabSettings = CreateTabButton("⚙️ Settings", false);
            btnTabSettings.Location = new Point(275, 3);
            btnTabSettings.Click += (s, e) => SwitchTab("settings");

            pnlTabs.Controls.Add(btnTabRecord);
            pnlTabs.Controls.Add(btnTabCloud);
            pnlTabs.Controls.Add(btnTabSettings);

            // Right Header Area (Anchored to Right, No Overlaps!)
            pnlHeaderRight = new Panel();
            pnlHeaderRight.Size = new Size(420, 40);
            pnlHeaderRight.Location = new Point(pnlHeader.Width - 430, 8);
            pnlHeaderRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlHeaderRight.BackColor = Color.Transparent;

            lblUserChip = new Label();
            lblUserChip.Text = "● Not Signed In";
            lblUserChip.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblUserChip.ForeColor = clrInkSec;
            lblUserChip.Location = new Point(10, 10);
            lblUserChip.Size = new Size(190, 22);
            lblUserChip.TextAlign = ContentAlignment.MiddleRight;
            lblUserChip.AutoEllipsis = true;

            btnWebPortal = new Button();
            btnWebPortal.Text = "Portal ↗";
            btnWebPortal.FlatStyle = FlatStyle.Flat;
            btnWebPortal.FlatAppearance.BorderColor = clrBorder;
            btnWebPortal.BackColor = clrSurface;
            btnWebPortal.ForeColor = clrInk;
            btnWebPortal.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnWebPortal.Size = new Size(80, 28);
            btnWebPortal.Location = new Point(210, 6);
            btnWebPortal.Cursor = Cursors.Hand;
            btnWebPortal.Click += (s, e) => { OpenAuthenticatedPortal(); };

            btnSignOut = new Button();
            btnSignOut.Text = "Sign Out";
            btnSignOut.FlatStyle = FlatStyle.Flat;
            btnSignOut.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnSignOut.BackColor = Color.FromArgb(254, 242, 242);
            btnSignOut.ForeColor = Color.FromArgb(220, 38, 38);
            btnSignOut.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnSignOut.Size = new Size(85, 28);
            btnSignOut.Location = new Point(300, 6);
            btnSignOut.Cursor = Cursors.Hand;
            btnSignOut.Visible = false;
            btnSignOut.Click += (s, e) => { ExecuteSignOut(); };

            pnlHeaderRight.Controls.Add(lblUserChip);
            pnlHeaderRight.Controls.Add(btnWebPortal);
            pnlHeaderRight.Controls.Add(btnSignOut);

            pnlHeader.Controls.Add(picBrand);
            pnlHeader.Controls.Add(lblBrand);
            pnlHeader.Controls.Add(lblVer);
            pnlHeader.Controls.Add(pnlTabs);
            pnlHeader.Controls.Add(pnlHeaderRight);

            // 3. Body View Area
            pnlBody = new Panel();
            pnlBody.Dock = DockStyle.Fill;

            BuildAuthTab();
            BuildCaptureTab();
            BuildCloudTab();
            BuildSettingsTab();

            pnlBody.Controls.Add(pnlAuthView);
            pnlBody.Controls.Add(pnlViewRecord);
            pnlBody.Controls.Add(pnlViewCloud);
            pnlBody.Controls.Add(pnlViewSettings);

            this.Controls.Add(pnlBody);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlUpdateBanner);
        }

        private Button CreateTabButton(string text, bool active)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(120, 30);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9F, active ? FontStyle.Bold : FontStyle.Regular);
            btn.BackColor = active ? clrSurface : Color.Transparent;
            btn.ForeColor = active ? clrInk : clrInkSec;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void SwitchTab(string tab)
        {
            if (!isAuthenticated)
            {
                ShowAuthView();
                return;
            }

            if(referenceActivePage==tab)return;
            pnlBody.SuspendLayout();
            Bitmap previousFrame=null;
            btnTabRecord.BackColor = Side;

            btnTabCloud.BackColor = Side;

            btnTabSettings.BackColor = Side;


            pnlAuthView.Visible = false;
            pnlViewRecord.Visible = false;
            pnlViewCloud.Visible = false;
            pnlViewSettings.Visible = false;

            if (tab == "record")
            {
                btnTabRecord.BackColor = clrSurface;

                pnlViewRecord.Visible = true;
            }
            else if (tab == "cloud")
            {
                btnTabCloud.BackColor = clrSurface;

                pnlViewCloud.Visible = true;
                ThreadPool.QueueUserWorkItem((s) => FetchCloudMeetings());
            }
            else if (tab == "settings")
            {
                btnTabSettings.BackColor = clrSurface;

                pnlViewSettings.Visible = true;
            }
            SyncReferenceShell(tab);
            ShowReferenceTransition(previousFrame,tab);
            pnlBody.ResumeLayout(true);
        }

        // ==========================================
        // TAB 0: AUTHENTICATION (SIGN IN / SIGN UP)
        // ==========================================
        private void BuildAuthTab()
        {
            pnlAuthView=new Panel {Dock=DockStyle.Fill,BackColor=Color.FromArgb(251,248,242),AutoScroll=true,Visible=false};
            pnlAuthView.Resize+=(sender,args)=>CenterAuthCard();
            pnlAuthCard=new RoundedPanel {Size=new Size(460,400),BackColor=Color.White,Padding=new Padding(32)};
            pnlAuthCard.Paint+=(sender,args)=>DrawRoundedBorder(args.Graphics,pnlAuthCard.ClientRectangle,20,clrBorder);
            lblAuthTitle=new Label {Text="Your meetings, together.",Font=new Font("Segoe UI",23,FontStyle.Bold),ForeColor=Color.FromArgb(15,42,46),Location=new Point(32,38),Size=new Size(396,66)};
            lblAuthSubtitle=new Label {Text="Sign in or create your Pactuno account.\nUse the same account across Briefly, Sonar and Relay.",Font=new Font("Segoe UI",10),ForeColor=clrInkSec,Location=new Point(32,112),Size=new Size(396,56)};
            btnAuthSubmit=new RoundedButton {Text="Sign in / Create account",Location=new Point(32,194),Size=new Size(396,48),BackColor=Color.FromArgb(15,42,46),ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)};
            btnAuthSubmit.FlatAppearance.BorderSize=0;btnAuthSubmit.Click+=(sender,args)=>BeginPactunoSignIn();
            var google=new RoundedButton {Text="Continue with Google",Outlined=true,Location=new Point(32,254),Size=new Size(396,46),BackColor=Color.White,ForeColor=Color.FromArgb(15,42,46),Font=new Font("Segoe UI",10)};
            google.Click+=(sender,args)=>BeginPactunoSignIn(true);
            lblAuthAlert=new Label {Text="Google or email verification opens in your secure browser.\nYou will return to this app after signing in.",Location=new Point(32,322),Size=new Size(396,58),ForeColor=clrInkSec,Font=new Font("Segoe UI",9)};
            pnlAuthCard.Controls.AddRange(new Control[]{lblAuthTitle,lblAuthSubtitle,btnAuthSubmit,google,lblAuthAlert});
            pnlAuthView.Controls.Add(pnlAuthCard);CenterAuthCard();
        }
        private void CenterAuthCard()
        {
            if(pnlAuthView==null||pnlAuthCard==null)return;
            pnlAuthCard.Location=new Point(Math.Max(16,(pnlAuthView.ClientSize.Width-pnlAuthCard.Width)/2),Math.Max(20,(pnlAuthView.ClientSize.Height-pnlAuthCard.Height)/2));
        }
        private void BeginPactunoSignIn(bool forceBrowser=false)
        {
            if(!btnAuthSubmit.Enabled)return;
            btnAuthSubmit.Enabled=false;
            authCancellation=new CancellationTokenSource();
            ThreadPool.QueueUserWorkItem(state=>{
                try{
                    if(forceBrowser||string.IsNullOrEmpty(pairToken))
                        pairToken=DesktopAuth.SignIn(message=>SafeUi(()=>lblAuthAlert.Text=message),authCancellation.Token);
                    VerifyCloudConnection();
                }catch(OperationCanceledException){}
                catch(Exception error){SafeUi(()=>{lblAuthAlert.Text="Sign-in could not complete. "+error.Message;lblAuthAlert.ForeColor=clrRed;});}
                finally{SafeUi(()=>btnAuthSubmit.Enabled=true);}
            });
        }
        private void SafeUi(Action action)
        {
            if(IsDisposed||!IsHandleCreated)return;
            try{BeginInvoke(new Action(()=>{if(!IsDisposed)action();}));}catch(InvalidOperationException){}
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(isRecording||syncRetryBusy!=0||(isAuthenticated&&btnRecord!=null&&!btnRecord.Enabled)){
                e.Cancel=true;
                MessageBox.Show("Stop recording and wait for processing to finish before closing Briefly. Your audio stays on this device.","Recording in progress",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }else{
                if(authCancellation!=null)authCancellation.Cancel();
                timerDetector.Stop();
                if(audioCapture!=null){audioCapture.Dispose();audioCapture=null;}
            }
            base.OnFormClosing(e);
        }

        private void OnAuthSuccess()
        {
            isAuthenticated = true;
            pnlTabs.Visible = true;
            btnSignOut.Visible = true;

            string username = currentUser != null && currentUser.ContainsKey("username") ? currentUser["username"] as string : "user";
            lblUserChip.Text = currentUser!=null&&currentUser.ContainsKey("email")?Convert.ToString(currentUser["email"]):username;
            lblUserChip.ForeColor = Ink;
            lblUserChip.ForeColor = clrGreen;

            if (lblSettingsUser != null && currentUser != null)
            {
                lblSettingsUser.Text = string.Format("Signed in as: @{0} ({1})", currentUser["username"], currentUser["email"]);
                lblSettingsStatus.Text = "● Cloud Workspace Synchronized";
                lblSettingsStatus.ForeColor = clrGreen;
            }

            if (lblCloudUser != null && currentUser != null)
            {
                lblCloudUser.Text = string.Format("Online Workspace: @{0}", currentUser["username"]);
            }

            SwitchTab("record");
            ThreadPool.QueueUserWorkItem((st) =>
            {
                FetchCloudMeetings();
                CheckRunningMeetings();
            });
        }

        private void ShowAuthView()
        {
            StopReferenceTransition();referenceActivePage=null;lastCloudFetch=DateTime.MinValue;
            isAuthenticated = false;
            cloudMeetingList.Clear();if(lvCloudMeetings!=null)lvCloudMeetings.Items.Clear();if(txtCloudTranscript!=null)txtCloudTranscript.Clear();if(txtCloudDetailSummary!=null)txtCloudDetailSummary.Clear();
            pnlTabs.Visible = false;
            btnSignOut.Visible = false;

            lblUserChip.Text = "● Not Signed In";
            lblUserChip.ForeColor = clrInkSec;

            pnlViewRecord.Visible = false;
            pnlViewCloud.Visible = false;
            pnlViewSettings.Visible = false;
            pnlAuthView.Visible = true;
            SyncReferenceShell(null);

            lblAuthAlert.Text="Sign in with your Pactuno account. New email accounts must be verified.";
        }

        private void ExecuteSignOut()
        {
            if(isRecording||syncRetryBusy!=0||(btnRecord!=null&&!btnRecord.Enabled)){MessageBox.Show("Stop recording and wait for processing or sync before signing out.","Briefly");return;}
            DialogResult dr = MessageBox.Show("Are you sure you want to sign out of Briefly?", "Sign Out", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                ClearConfig();
                cloudMeetingList.Clear();
                if (lvCloudMeetings != null) lvCloudMeetings.Items.Clear();
                ShowAuthView();
            }
        }

        // ==========================================
        // TAB 1: RECORD & DETECTOR
        // ==========================================
        private void BuildCaptureTab()
        {
            pnlViewRecord = new Panel { Dock = DockStyle.Fill, BackColor = clrBg, Visible = false };

            // Left Sidebar Card (360px)
            Panel pnlLeft = new Panel { Dock = DockStyle.Left, Width = 360, BackColor = clrSurface, Padding = new Padding(24) };
            pnlLeft.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(clrBorder), pnlLeft.Width - 1, 0, pnlLeft.Width - 1, pnlLeft.Height); };

            // Active Meeting Radar Card
            Panel pnlRadar = new Panel { Size = new Size(312, 72), Location = new Point(24, 20), BackColor = Color.FromArgb(249, 250, 251), Padding = new Padding(12) };
            pnlRadar.Paint += (s, e) => { DrawRoundedBorder(e.Graphics, pnlRadar.ClientRectangle, 10, clrBorderLight); };

            picRadarDot = new PictureBox { Size = new Size(12, 12), Location = new Point(14, 18) };
            picRadarDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(isMeetingDetected ? clrGreen : clrInkSec))
                    e.Graphics.FillEllipse(br, 1, 1, 10, 10);
            };

            lblRadarText = new Label
            {
                Text = "ACTIVE MEETING RADAR\nListening for Google Meet, Teams & Zoom...",
                Font = new Font("Segoe UI", 8F),
                ForeColor = clrInkSec,
                Location = new Point(32, 14),
                Size = new Size(265, 42)
            };

            pnlRadar.Controls.Add(picRadarDot);
            pnlRadar.Controls.Add(lblRadarText);

            // Waveform Visualizer
            pnlWaveform = new Panel { Size = new Size(312, 100), Location = new Point(24, 108), BackColor = Color.FromArgb(249, 250, 251) };
            pnlWaveform.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                DrawRoundedBorder(e.Graphics, pnlWaveform.ClientRectangle, 12, clrBorderLight);

                int count = waveHeights.Length;
                int barWidth = 6;
                int gap = 5;
                int totalWidth = count * barWidth + (count - 1) * gap;
                int startX = (pnlWaveform.Width - totalWidth) / 2;
                int centerY = pnlWaveform.Height / 2;

                for (int i = 0; i < count; i++)
                {
                    int h = waveHeights[i];
                    int x = startX + i * (barWidth + gap);
                    int y = centerY - h / 2;
                    Color barClr = isRecording ? Color.FromArgb(17, 24, 39) : Color.FromArgb(209, 213, 219);
                    using (var br = new SolidBrush(barClr))
                    {
                        FillRoundedRectangle(e.Graphics, br, new Rectangle(x, y, barWidth, h), 3);
                    }
                }
            };

            // Digital Timer
            lblTimer = new Label
            {
                Text = "00:00:00",
                Font = new Font("Segoe UI", 26F, FontStyle.Bold),
                ForeColor = clrInk,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(312, 48),
                Location = new Point(24, 222)
            };

            // Record / Finish Pill Button
            btnRecord = new Button
            {
                Text = "● Start Intelligence Capture",
                Size = new Size(312, 44),
                Location = new Point(24, 280),
                FlatStyle = FlatStyle.Flat,
                BackColor = clrAccent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRecord.FlatAppearance.BorderSize = 0;
            btnRecord.Click += BtnRecord_Click;

            // Status message
            lblStatus = new Label
            {
                Text = "Ready · Loopback WASAPI Audio Capture active",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = clrInkSec,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(312, 36),
                Location = new Point(24, 332)
            };

            // Audio features list
            Label lblSpecs = new Label
            {
                Text = "FEATURES:\n• Dual WASAPI Audio Capture (Mic + Speakers)\n• English & Urdu Multi-Language Speech Intelligence\n• Direct Zero-Bot Recording (100% Private)",
                Font = new Font("Segoe UI", 8F),
                ForeColor = clrInkSec,
                Location = new Point(24, 390),
                Size = new Size(312, 80)
            };

            pnlLeft.Controls.Add(pnlRadar);
            pnlLeft.Controls.Add(pnlWaveform);
            pnlLeft.Controls.Add(lblTimer);
            pnlLeft.Controls.Add(btnRecord);
            pnlLeft.Controls.Add(lblStatus);
            pnlLeft.Controls.Add(lblSpecs);

            // Right Sheet: Real-time Notes
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = clrBg, Padding = new Padding(24) };
            Panel pnlSheet = new Panel { Dock = DockStyle.Fill, BackColor = clrSurface, Padding = new Padding(24) };
            pnlSheet.Paint += (s, e) => { DrawRoundedBorder(e.Graphics, pnlSheet.ClientRectangle, 14, clrBorder); };

            Label lblSheetTitle = new Label { Text = "Synthesized Meeting Intelligence", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = clrInk, Location = new Point(24, 20), AutoSize = true };
            Label lblSheetSub = new Label { Text = "Executive Summary, Decisions, and Action Items sync automatically to pactuno.online.", Font = new Font("Segoe UI", 9F), ForeColor = clrInkSec, Location = new Point(26, 48), AutoSize = true };

            txtLocalNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 250, 252),
                Font = new Font("Consolas", 9.5F),
                Location = new Point(26, 80),
                Size = new Size(580, 480),
                Text = "Briefly Zero-Bot Meeting Intelligence\r\n=========================================\r\nPress 'Start Intelligence Capture' or begin your Google Meet/Teams meeting to capture audio."
            };

            pnlSheet.Controls.Add(lblSheetTitle);
            pnlSheet.Controls.Add(lblSheetSub);
            pnlSheet.Controls.Add(txtLocalNotes);
            pnlRight.Controls.Add(pnlSheet);

            pnlViewRecord.Controls.Add(pnlRight);
            pnlViewRecord.Controls.Add(pnlLeft);
            liveListen=new ListenSwitch { Text="Live Listen — ask me when a meeting is detected",Dock=DockStyle.Top,Height=44,Padding=new Padding(24,0,0,0),BackColor=clrSurface,ForeColor=clrInk,AutoCheck=true,Checked=false };
            liveListen.AccessibleDescription="Optional supported-call detection. Recording begins only after confirmation. System capture includes all sound on the selected output device.";
            liveListen.CheckedChanged+=(sender,args)=>{
                if(restoringListen)return;
                if(liveListen.Checked&&MessageBox.Show("Enable detection while Briefly is running, including when minimized? Briefly checks other apps' microphone-use metadata and supported call controls. It does not listen to or record audio until you choose Start. Microphone use can be something other than a meeting, and some calls may be missed.","Live Listen",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){restoringListen=true;liveListen.Checked=false;restoringListen=false;}
                detectorGeneration++;listenState.Update(false,isRecording,false);liveListenEnabled=liveListen.Checked;latestMeetingSignal=new MeetingSignal();listenPreferences.Enabled=liveListenEnabled;
                SaveListenPreferences();DesktopDiagnostics.Detection=liveListenEnabled?"Waiting for next poll":"Off";DesktopDiagnostics.Record("detection","preferences",liveListenEnabled?"enabled":"disabled");
            };
            pnlViewRecord.Controls.Add(liveListen);
            liveListen.BringToFront();
        }

        // ==========================================
        // TAB 2: CLOUD WORKSPACE (ONLINE RETRIEVAL)
        // ==========================================
        private void BuildCloudTab()
        {
            pnlViewCloud = new Panel { Dock = DockStyle.Fill, BackColor = clrBg, Visible = false };

            // Left Sidebar List (360px)
            Panel pnlCloudLeft = new Panel { Dock = DockStyle.Left, Width = 360, BackColor = clrSurface, Padding = new Padding(16) };
            pnlCloudLeft.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(clrBorder), pnlCloudLeft.Width - 1, 0, pnlCloudLeft.Width - 1, pnlCloudLeft.Height); };

            lblCloudUser = new Label { Text = "Online Workspace", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = clrInk, Location = new Point(16, 12), AutoSize = true };
            lblCloudCount = new Label { Text = "0 meetings retrieved from Cloudflare D1", Font = new Font("Segoe UI", 8F), ForeColor = clrInkSec, Location = new Point(16, 32), AutoSize = true };

            Button btnSync = new Button { Text = "Sync from Cloud 🔄", Size = new Size(328, 28), Location = new Point(16, 56), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = clrInk, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btnSync.FlatAppearance.BorderColor = clrBorder;
            btnSync.Click += (s, e) => { ThreadPool.QueueUserWorkItem((state) => FetchCloudMeetings(true)); };

            lvCloudMeetings = new ListView { Location = new Point(16, 94), Size = new Size(328, 500), View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            lvCloudMeetings.Columns.Add("Meeting Title", 220);
            lvCloudMeetings.Columns.Add("Date", 100);
            lvCloudMeetings.SelectedIndexChanged += LvCloudMeetings_SelectedIndexChanged;

            pnlCloudLeft.Controls.Add(lblCloudUser);
            pnlCloudLeft.Controls.Add(lblCloudCount);
            pnlCloudLeft.Controls.Add(btnSync);
            pnlCloudLeft.Controls.Add(lvCloudMeetings);

            // Right Inspector Pane
            Panel pnlCloudRight = new Panel { Dock = DockStyle.Fill, BackColor = clrBg, Padding = new Padding(24) };
            Panel pnlSheet = new Panel { Dock = DockStyle.Fill, BackColor = clrSurface, Padding = new Padding(20) };
            pnlSheet.Paint += (s, e) => { DrawRoundedBorder(e.Graphics, pnlSheet.ClientRectangle, 14, clrBorder); };

            lblCloudDetailTitle = new Label { Text = "Select a meeting from the online archive", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = clrInk, Location = new Point(20, 16), AutoSize = true };
            lblCloudDetailMeta = new Label { Text = "Synced directly from https://briefly.pactuno.online/api/meetings", Font = new Font("Segoe UI", 9F), ForeColor = clrInkSec, Location = new Point(22, 46), AutoSize = true };

            Label lblSumm = new Label { Text = "EXECUTIVE SUMMARY", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = clrInkSec, Location = new Point(22, 78), AutoSize = true };
            txtCloudDetailSummary = new TextBox { Location = new Point(22, 98), Size = new Size(590, 80), Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(250, 250, 252), Font = new Font("Segoe UI", 9.5F), ScrollBars = ScrollBars.Vertical };

            Label lblActs = new Label { Text = "ACTION ITEMS", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = clrInkSec, Location = new Point(22, 190), AutoSize = true };
            lvCloudDetailActions = new ListView { Location = new Point(22, 210), Size = new Size(590, 130), View = View.Details, CheckBoxes = true, FullRowSelect = true };
            lvCloudDetailActions.Columns.Add("Action Item", 400);
            lvCloudDetailActions.Columns.Add("Assignee / Due", 170);

            Label lblTrans = new Label { Text = "VERBATIM TRANSCRIPT", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = clrInkSec, Location = new Point(22, 350), AutoSize = true };
            txtCloudTranscript = new TextBox { Location = new Point(22, 370), Size = new Size(590, 130), Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(250, 250, 252), Font = new Font("Consolas", 8.5F), ScrollBars = ScrollBars.Vertical };

            Button btnCloudWeb = new Button { Text = "Open on pactuno.online ↗", Size = new Size(180, 32), Location = new Point(22, 515), FlatStyle = FlatStyle.Flat, BackColor = clrAccent, ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnCloudWeb.FlatAppearance.BorderSize = 0;
            btnCloudWeb.Click += (s, e) => { Process.Start(apiOrigin + "/app/"); };

            pnlSheet.Controls.Add(lblCloudDetailTitle);
            pnlSheet.Controls.Add(lblCloudDetailMeta);
            pnlSheet.Controls.Add(lblSumm);
            pnlSheet.Controls.Add(txtCloudDetailSummary);
            pnlSheet.Controls.Add(lblActs);
            pnlSheet.Controls.Add(lvCloudDetailActions);
            pnlSheet.Controls.Add(lblTrans);
            pnlSheet.Controls.Add(txtCloudTranscript);
            pnlSheet.Controls.Add(btnCloudWeb);

            pnlCloudRight.Controls.Add(pnlSheet);
            pnlViewCloud.Controls.Add(pnlCloudRight);
            pnlViewCloud.Controls.Add(pnlCloudLeft);
        }

        // ==========================================
        // TAB 3: SETTINGS
        // ==========================================
        private void BuildSettingsTab()
        {
            pnlViewSettings = new Panel { Dock = DockStyle.Fill, BackColor = clrBg, Padding = new Padding(40), Visible = false };

            Panel pnlCard = new Panel { Size = new Size(600, 480), Location = new Point(40, 20), BackColor = clrSurface, Padding = new Padding(24) };
            pnlCard.Paint += (s, e) => { DrawRoundedBorder(e.Graphics, pnlCard.ClientRectangle, 14, clrBorder); };

            Label lblSetHead = new Label { Text = "Briefly Account & Cloud Sync", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = clrInk, Location = new Point(24, 20), AutoSize = true };
            lblSettingsUser = new Label { Text = "Signed in as: Loading...", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = clrInk, Location = new Point(26, 52), AutoSize = true };
            lblSettingsStatus = new Label { Text = "● Status: Synchronized", Font = new Font("Segoe UI", 9F), ForeColor = clrGreen, Location = new Point(26, 76), AutoSize = true };

            // Token input if needed
            Label lblTok = new Label { Text = "MACHINE PAIR TOKEN", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = clrInkSec, Location = new Point(26, 115), AutoSize = true };
            txtPairToken = new TextBox { Text = "Managed securely by Pactuno ID", Location = new Point(26, 135), Size = new Size(540, 26), Font = new Font("Segoe UI", 9.5F), ReadOnly = true };

            Button btnManualSignOut = new Button { Text = "Sign Out of this Machine", Size = new Size(200, 32), Location = new Point(26, 180), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(254, 242, 242), ForeColor = Color.FromArgb(220, 38, 38), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btnManualSignOut.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnManualSignOut.Click += (s, e) => { ExecuteSignOut(); };
            Button retry=new RoundedButton {Text="Retry saved meetings",Size=new Size(210,36),Location=new Point(240,178),BackColor=Color.FromArgb(15,42,46),ForeColor=Color.White,Font=new Font("Segoe UI",9F)};
            retry.Click+=(sender,args)=>RetrySavedMeetings(retry);
            pnlCard.Controls.Add(retry);

            // Version info & Update
            Label lblVerHead = new Label { Text = "SOFTWARE VERSION & UPDATES", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = clrInkSec, Location = new Point(26, 245), AutoSize = true };
            lblVersionInfo = new Label { Text = "Current Version: v" + appVersion + " (Windows Native 64-bit)", Font = new Font("Segoe UI", 9.5F), ForeColor = clrInk, Location = new Point(26, 268), AutoSize = true };

            Button btnCheckUpdate = new Button { Text = "Check for Updates Online", Size = new Size(180, 32), Location = new Point(26, 305), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = clrInk, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btnCheckUpdate.FlatAppearance.BorderColor = clrBorder;
            btnCheckUpdate.Click += (s, e) => { ThreadPool.QueueUserWorkItem((st) => CheckForAppUpdates(true)); };

            pnlCard.Controls.Add(lblSetHead);
            pnlCard.Controls.Add(lblSettingsUser);
            pnlCard.Controls.Add(lblSettingsStatus);
            pnlCard.Controls.Add(lblTok);
            pnlCard.Controls.Add(txtPairToken);
            pnlCard.Controls.Add(btnManualSignOut);
            pnlCard.Controls.Add(lblVerHead);
            pnlCard.Controls.Add(lblVersionInfo);
            pnlCard.Controls.Add(btnCheckUpdate);
            var speechSettings=new RoundedButton{Text="Speech recognition settings",Location=new Point(26,355),Size=new Size(270,42),BackColor=Color.FromArgb(15,42,46),ForeColor=Color.White};
            speechSettings.Click+=(sender,args)=>{if(isRecording){MessageBox.Show("Finish recording before changing recognition settings.","Briefly");return;}SpeechPreferences.ShowDialog(this);};
            pnlCard.Controls.Add(speechSettings);

            pnlViewSettings.Controls.Add(pnlCard);
        }

        private void ApplyCustomStyles()
        {
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        private void DrawRoundedBorder(Graphics g, Rectangle rect, int radius, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d - 1, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d - 1, rect.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                using (Pen p = new Pen(color, 1))
                {
                    g.DrawPath(p, path);
                }
            }
        }

        private void FillRoundedRectangle(Graphics g, Brush b, Rectangle rect, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d - 1, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d - 1, rect.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }

        // ==========================================
        // RECORDING ACTIONS & AUDIO ENGINE
        // ==========================================
        private void BtnRecord_Click(object sender, EventArgs e)
        {
            if(!btnRecord.Enabled)return;
            if (!isRecording)
            {
                try {
                    DesktopSpeech.VerifyInstalled(AppDomain.CurrentDomain.BaseDirectory);
                    audioCapture=new CaptureSession();audioCapture.Start(recordingsDir);
                } catch(Exception error) {
                    DesktopDiagnostics.Capture="Could not start";DesktopDiagnostics.LastError="Capture could not start; check microphone/output and installed speech engine";DesktopDiagnostics.Record("capture","wasapi","start-failed");
                    if(audioCapture!=null){audioCapture.Dispose();audioCapture=null;}
                    lblStatus.Text=error.Message;lblStatus.ForeColor=clrRed;return;
                }
                isRecording = true;
                recordingStartTime = DateTime.UtcNow;
                capturedStartedAt=recordingStartTime;
                capturedMeetingTitle=latestMeetingSignal.ObservedTitle;
                capturedPlatform=latestMeetingSignal.Platform;
                capturedSignalKey=latestMeetingSignal.Key;
                listenState.RecordingStarted(recordingFromDetection);
                DesktopDiagnostics.Capture="Recording";DesktopDiagnostics.Record("capture","wasapi","started");
                btnRecord.Text = "Stop & save";
                btnRecord.BackColor = clrRed;
                btnRecord.ForeColor = Color.White;
                lblStatus.Text = "● Recording Active (Dual WASAPI Loopback + Mic)";
                lblStatus.ForeColor = clrRed;
                timerClock.Start();
                timerWave.Start();
            }
            else
            {
                listenState.ManualStop();
                // Stop and process the captured audio.
                isRecording = false;
                timerClock.Stop();
                timerWave.Stop();
                for (int i = 0; i < waveHeights.Length; i++) waveHeights[i] = 6;
                pnlWaveform.Invalidate();

                btnRecord.Enabled = false;
                btnRecord.Text = "Saving audio...";
                lblStatus.Text = "Stopping capture and saving your audio. Please wait...";
                DesktopDiagnostics.Capture="Saving audio";DesktopDiagnostics.Record("capture","wasapi","stop-requested");

                ThreadPool.QueueUserWorkItem((state) =>
                {
                    SynthesizeFinishedMeeting();
                });
            }
        }

        private void TimerClock_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.UtcNow - recordingStartTime;
            lblTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
        }

        private void TimerWave_Tick(object sender, EventArgs e)
        {
            if(referenceWave!=null)referenceWave.Push(audioCapture==null?0f:audioCapture.Peak);
            for (int i = 0; i < waveHeights.Length; i++)
            {
                waveHeights[i] = 6+(int)(Math.Min(1f,audioCapture==null?0f:audioCapture.Peak)*65);
            }
            pnlWaveform.Invalidate();
        }

        private void SynthesizeFinishedMeeting()
        {
            string stamp=DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
            string wavPath=Path.Combine(recordingsDir,"meeting_"+stamp+".wav");
            string mp3Path=Path.Combine(recordingsDir,"meeting_"+stamp+".mp3");
            try {
                audioCapture.Stop(wavPath,mp3Path);
                double capturedSeconds=audioCapture.ElapsedSeconds;
                audioCapture.Dispose();audioCapture=null;
                DesktopDiagnostics.Capture="Transcribing locally";DesktopDiagnostics.Record("transcription","whisper","started");
                this.BeginInvoke(new Action(()=>{btnRecord.Text="Transcribing...";lblStatus.Text="Audio saved. Transcribing on this computer...";}));
                string transcript=DesktopSpeech.Transcribe(AppDomain.CurrentDomain.BaseDirectory,wavPath);
                DesktopDiagnostics.Record("transcription","whisper","completed");
                string title=string.IsNullOrWhiteSpace(capturedMeetingTitle)?"":capturedMeetingTitle;
                var payload=new Dictionary<string,object>{{"requestId","recording_"+Guid.NewGuid().ToString("N")},{"windowTitle",title},{"transcript",transcript},{"durationSeconds",capturedSeconds},{"source","windows_desktop"},{"platform",capturedPlatform??"Unknown"},{"recordedAt",new DateTimeOffset(capturedStartedAt).ToUnixTimeMilliseconds()}};
                JavaScriptSerializer json=new JavaScriptSerializer();json.MaxJsonLength=8*1024*1024;
                string pending=Path.Combine(recordingsDir,"meeting_"+stamp+".pending.json");
                string owner=currentUser!=null&&currentUser.ContainsKey("id")?Convert.ToString(currentUser["id"]):null;
                PendingUpload.Save(pending,owner,json.Serialize(payload));
                this.BeginInvoke(new Action(()=>{btnRecord.Text="Syncing notes...";lblStatus.Text="Transcript ready. Syncing your meeting...";}));
                DesktopDiagnostics.Capture="Syncing notes";DesktopDiagnostics.Record("sync","portal","started");
                bool synced=false;string syncMessage="Saved locally. Upload will need to be retried.";
                try {
                    if(!isAuthenticated||string.IsNullOrEmpty(pairToken))throw new IOException("Sign in to sync.");
                    HttpWebRequest request=(HttpWebRequest)WebRequest.Create(apiOrigin+"/api/meetings");
                    request.Method="POST";request.ContentType="application/json";SetAuthentication(request);request.Timeout=30000;
                    byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(payload));request.ContentLength=bytes.Length;
                    using(Stream body=request.GetRequestStream())body.Write(bytes,0,bytes.Length);
                    using(HttpWebResponse response=(HttpWebResponse)request.GetResponse())
                    using(StreamReader reader=new StreamReader(response.GetResponseStream())) {
                        var saved=json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                        synced=saved!=null&&saved.ContainsKey("ok")&&(bool)saved["ok"]&&saved.ContainsKey("meeting");
                    }
                    if(!synced)throw new IOException("The server did not confirm the meeting.");
                    File.Move(pending,pending.Replace(".pending.json",".synced.json"));syncMessage="Transcript synced. Open the web portal for meeting notes.";
                } catch { DesktopDiagnostics.LastError="Sync not confirmed; recording and pending transcript kept"; }
                DesktopDiagnostics.Capture=synced?"Idle - synced":"Idle - sync pending";DesktopDiagnostics.Record("sync","portal",synced?"confirmed":"pending");
                this.BeginInvoke(new Action(()=>{
                    ResetRecordButton();
                    lblStatus.Text=syncMessage;lblStatus.ForeColor=synced?clrGreen:clrRed;lblTimer.Text="00:00:00";
                    txtLocalNotes.Text="Audio saved: "+mp3Path+"\r\n\r\nTRANSCRIPT\r\n"+transcript+"\r\n\r\n"+syncMessage;
                    if(synced)ThreadPool.QueueUserWorkItem(_=>FetchCloudMeetings());
                }));
            } catch(Exception error) {
                DesktopDiagnostics.Capture="Failed - local files preserved";DesktopDiagnostics.LastError="Capture or transcription failed; review app status";DesktopDiagnostics.Record("processing","local","failed");
                if(audioCapture!=null){audioCapture.Dispose();audioCapture=null;}
                this.BeginInvoke(new Action(()=>{ResetRecordButton();lblStatus.Text=error.Message;lblStatus.ForeColor=clrRed;}));
            }
        }

        // ==========================================
        // CLOUD API: STATUS & VERIFICATION
        // ==========================================
        private void SetAuthentication(HttpWebRequest request)
        {
            string access=DesktopAuth.AccessToken();
            if(string.IsNullOrEmpty(access))throw new IOException("Sign in to continue.");
            request.Headers["Authorization"]="Bearer "+access;
            request.Headers["X-Briefly-Client"]="briefly-desktop";
            request.AllowAutoRedirect=false;
        }

        private void RetrySavedMeetings(Button button)
        {
            if(!isAuthenticated||currentUser==null||!currentUser.ContainsKey("id")){MessageBox.Show("Sign in before retrying saved meetings.","Briefly");return;}
            if(isRecording||!btnRecord.Enabled||Interlocked.CompareExchange(ref syncRetryBusy,1,0)!=0)return;
            string owner=Convert.ToString(currentUser["id"]);button.Enabled=false;
            ThreadPool.QueueUserWorkItem(state=>{
                int sent=0,skipped=0,failed=0;
                try{
                    foreach(string file in Directory.GetFiles(recordingsDir,"*.pending.json")){
                        string payload;try{payload=PendingUpload.ReadOwned(file,owner);}catch{skipped++;continue;}
                        try{
                            var request=(HttpWebRequest)WebRequest.Create(apiOrigin+"/api/meetings");request.Method="POST";request.ContentType="application/json";request.Timeout=30000;SetAuthentication(request);
                            byte[] bytes=Encoding.UTF8.GetBytes(payload);request.ContentLength=bytes.Length;using(var body=request.GetRequestStream())body.Write(bytes,0,bytes.Length);
                            using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){
                                var saved=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                                if(saved==null||!saved.ContainsKey("ok")||!(bool)saved["ok"]||!saved.ContainsKey("meeting"))throw new IOException("Sync was not confirmed.");
                            }
                            File.Move(file,file.Replace(".pending.json",".synced.json"));sent++;
                        }catch{failed++;}
                    }
                    SafeUi(()=>lblSettingsStatus.Text=sent+" saved meeting(s) synced; "+failed+" waiting; "+skipped+" from another or unverified profile left untouched.");
                    if(sent>0)FetchCloudMeetings();
                }catch{SafeUi(()=>lblSettingsStatus.Text="Saved meetings could not be checked. Local files are preserved.");}
                finally{Interlocked.Exchange(ref syncRetryBusy,0);SafeUi(()=>button.Enabled=true);}
            });
        }

        private void VerifyCloudConnection()
        {
            if (string.IsNullOrEmpty(pairToken))
            {
                this.BeginInvoke(new Action(() => ShowAuthView()));
                return;
            }

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiOrigin + "/api/desktop/status");
                req.Timeout = 20000;
                SetAuthentication(req);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("user"))
                    {
                        currentUser = dict["user"] as Dictionary<string, object>;
                        this.BeginInvoke(new Action(() => OnAuthSuccess()));
                        return;
                    }
                }
            }
            catch(WebException error)
            {
                var response=error.Response as HttpWebResponse;
                if(response!=null&&(response.StatusCode==HttpStatusCode.Unauthorized||response.StatusCode==HttpStatusCode.Forbidden)){
                    ClearConfig();SafeUi(()=>ShowAuthView());
                }else if(response!=null&&response.StatusCode==HttpStatusCode.Conflict){
                    string account=null;try{account=DesktopAuth.SignedInEmail();}catch{}
                    SafeUi(()=>{btnAuthSubmit.Text="Retry workspace connection";lblAuthAlert.Text=(string.IsNullOrEmpty(account)?"":("Pactuno account: "+account+". "))+"Link this same account in Portal > Desktop app, then retry. To use another account, choose Continue with Google.";});
                }else{
                    SafeUi(()=>{lblAuthAlert.Text="Could not reach your workspace. Your sign-in is saved; try again when the connection returns.";lblStatus.Text="Workspace is temporarily unavailable. Local recordings are preserved.";});
                }
            }
            catch(Exception)
            {
                SafeUi(()=>lblAuthAlert.Text="Could not verify your workspace. Please try again.");
            }
        }

        // ==========================================
        // CLOUD API: MEETINGS LIST & RETRIEVAL
        // ==========================================
        private DateTime lastCloudFetch=DateTime.MinValue;
        private int cloudFetchBusy;
        private void FetchCloudMeetings(bool notifyUser = false)
        {
            if (!isAuthenticated || string.IsNullOrEmpty(pairToken)) return;
            if(!notifyUser&&DateTime.UtcNow-lastCloudFetch<TimeSpan.FromSeconds(30))return;
            if(Interlocked.CompareExchange(ref cloudFetchBusy,1,0)!=0)return;
            string fetchOwner=currentUser!=null&&currentUser.ContainsKey("id")?Convert.ToString(currentUser["id"]):null;

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiOrigin + "/api/meetings?_t=" + DateTime.UtcNow.Ticks);
                req.Timeout = 6000;
                SetAuthentication(req);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);

                    if (dict != null && dict.ContainsKey("meetings"))
                    {
                        ArrayList arr = dict["meetings"] as ArrayList;
                        if (arr != null)
                        {
                            var loaded=new List<Dictionary<string,object>>();
                            foreach (object item in arr)
                            {
                                if (item is Dictionary<string, object>)
                                {
                                    loaded.Add((Dictionary<string, object>)item);
                                }
                            }
                            this.BeginInvoke(new Action(()=>{if(!isAuthenticated||currentUser==null||!currentUser.ContainsKey("id")||Convert.ToString(currentUser["id"])!=fetchOwner)return;cloudMeetingList.Clear();cloudMeetingList.AddRange(loaded);lastCloudFetch=DateTime.UtcNow;PopulateCloudMeetingsList();}));
                        }

                        this.BeginInvoke(new Action(() =>
                        {
                            if (notifyUser) MessageBox.Show("Retrieved " + cloudMeetingList.Count + " meetings from Cloudflare D1.", "Briefly Cloud Sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                if (notifyUser)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Cloud sync error: " + ex.Message, "Briefly", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
            }
            finally{Interlocked.Exchange(ref cloudFetchBusy,0);}
        }

        private void PopulateCloudMeetingsList()
        {
            if (lvCloudMeetings == null) return;
            lvCloudMeetings.BeginUpdate();
            lvCloudMeetings.Items.Clear();

            lblCloudCount.Text = string.Format("{0} {1} in your workspace", cloudMeetingList.Count,cloudMeetingList.Count==1?"meeting":"meetings");

            foreach (var m in cloudMeetingList)
            {
                string title = m.ContainsKey("title") ? m["title"] as string : "Meeting";
                string dateStr = "Recent";
                if (m.ContainsKey("createdAt") && m["createdAt"] != null)
                {
                    try
                    {
                        long ms = Convert.ToInt64(m["createdAt"]);
                        DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime();
                        dateStr = dt.ToString("MMM dd");
                    }
                    catch { }
                }

                ListViewItem item = new ListViewItem(title);
                item.SubItems.Add(dateStr);
                item.Tag = m;
                lvCloudMeetings.Items.Add(item);
            }

            if (lvCloudMeetings.Items.Count > 0)
            {
                lvCloudMeetings.Items[0].Selected = true;
            }
            lvCloudMeetings.EndUpdate();
        }

        private void LvCloudMeetings_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvCloudMeetings.SelectedItems.Count == 0) return;
            var m = lvCloudMeetings.SelectedItems[0].Tag as Dictionary<string, object>;
            if (m == null) return;

            string title = m.ContainsKey("title") ? m["title"] as string : "Meeting";
            string client = m.ContainsKey("clientName") ? m["clientName"] as string : "Pactuno Client";
            string summary = m.ContainsKey("summary") ? m["summary"] as string : "";
            string transcript = m.ContainsKey("transcript") ? m["transcript"] as string : "No verbatim transcript stored.";

            lblCloudDetailTitle.Text = title;
            lblCloudDetailMeta.Text = string.IsNullOrWhiteSpace(client)?"Saved transcript and notes":client+" · Saved meeting";
            txtCloudDetailSummary.Text = summary;
            txtCloudTranscript.Text = transcript;

            lvCloudDetailActions.Items.Clear();
            if (m.ContainsKey("actionItems") && m["actionItems"] is ArrayList)
            {
                ArrayList acts = (ArrayList)m["actionItems"];
                foreach (var obj in acts)
                {
                    if (obj is Dictionary<string, object>)
                    {
                        var d = (Dictionary<string, object>)obj;
                        string actText = d.ContainsKey("text") ? d["text"] as string : "";
                        string assignee = d.ContainsKey("assignee") ? d["assignee"] as string : "";
                        string due = d.ContainsKey("due") ? d["due"] as string : "";
                        bool completed = d.ContainsKey("completed") ? Convert.ToBoolean(d["completed"]) : false;

                        ListViewItem lvi = new ListViewItem(actText);
                        lvi.SubItems.Add(string.IsNullOrEmpty(assignee) ? due : assignee + " (" + due + ")");
                        lvi.Checked = completed;
                        lvCloudDetailActions.Items.Add(lvi);
                    }
                }
            }
        }

        // ==========================================
        // BLACKOUT-STYLE UPDATE CHECKER
        // ==========================================
        private void CheckForAppUpdates(bool notifyUser = false)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiOrigin + "/api/version?_t=" + DateTime.UtcNow.Ticks);
                req.Timeout = 4000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("latestVersion"))
                    {
                        string remoteVer = dict["latestVersion"] as string;
                        if (IsVersionNewer(remoteVer, appVersion))
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                lblUpdateMsg.Text = "New version v"+remoteVer+" available. Click Download update.";
                                pnlUpdateBanner.Visible = true;
                            }));
                            return;
                        }
                    }
                }

                if (notifyUser)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("You are running the latest version of Briefly (v" + appVersion + ").", "Briefly Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
            }
            catch (Exception ex)
            {
                if (notifyUser)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Could not check for updates: " + ex.Message, "Briefly", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
            }
        }

        private bool IsVersionNewer(string remote, string current)
        {
            try
            {
                string[] rParts = remote.TrimStart('v').Split('.');
                string[] cParts = current.TrimStart('v').Split('.');
                for (int i = 0; i < 3; i++)
                {
                    int r = i < rParts.Length ? int.Parse(rParts[i]) : 0;
                    int c = i < cParts.Length ? int.Parse(cParts[i]) : 0;
                    if (r != c) return r > c;
                }
            }
            catch { }
            return false;
        }

        // ==========================================
        // ACTIVE MEETING RADAR PROCESS DETECTOR
        // ==========================================
        private void ResetRecordButton()
        {
            btnRecord.BackColor=Amber;btnRecord.ForeColor=Ink;btnRecord.Enabled=true;btnRecord.Text="Record meeting";
            if(referenceCaptureTitle!=null)referenceCaptureTitle.Text="Ready to capture";
        }
        private void SaveListenPreferences(){
            try{listenPreferences.SaveTo(ListenPreferences.Store);}
            catch{DesktopDiagnostics.LastError="Live Listen preferences could not be saved";DesktopDiagnostics.Record("preferences","local","save-failed");lblStatus.Text="Your Live Listen change is active for this session, but could not be saved.";}
        }
        private string ShowDetectedMeetingPrompt(MeetingSignal signal){
            string result="ignore";
            using(var dialog=new Form{Text="Possible meeting detected",ClientSize=new Size(600,254),MinimumSize=new Size(480,254),StartPosition=FormStartPosition.CenterParent,BackColor=Paper,ForeColor=Ink,Font=new Font("Segoe UI",10),MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog}){
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));dialog.Controls.Add(layout);
                layout.Controls.Add(RefLabel("Start recording this meeting?",18,true),0,0);
                var message=RefLabel(signal.Source=="microphone"?"Another app is using your microphone. This may be a meeting or another voice feature.":"Supported active-call controls were found.",10);
                message.Text+="\nRecording captures your mic and system sound. Inform participants first.";layout.Controls.Add(message,0,1);
                var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};layout.Controls.Add(actions,0,2);
                var start=RefButton("Start",()=>{result="start";dialog.Close();},true);start.Width=110;
                var ignore=RefButton("Ignore",()=>dialog.Close());ignore.Width=110;
                var never=RefButton("Don't ask for this app",()=>{result="ignore-app";dialog.Close();});never.Width=240;
                actions.Controls.AddRange(new Control[]{start,ignore,never});dialog.CancelButton=ignore;dialog.Shown+=(s,e)=>ignore.Focus();dialog.ShowDialog(this);
            }
            return result;
        }
        private void ShowDiagnostics(){
            using(var dialog=new Form{Text="Briefly diagnostics",ClientSize=new Size(650,470),MinimumSize=new Size(480,360),StartPosition=FormStartPosition.CenterParent,BackColor=Paper,Font=new Font("Segoe UI",10)}){
                var box=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,BackColor=Paper,ForeColor=Ink,Text=DesktopDiagnostics.Snapshot()};dialog.Controls.Add(box);
                using(var refresh=new System.Windows.Forms.Timer{Interval=1000}){refresh.Tick+=(s,e)=>{string value=DesktopDiagnostics.Snapshot();if(value!=box.Text)box.Text=value;};refresh.Start();dialog.ShowDialog(this);}
            }
        }
        private void ResetIgnoredApps(){
            if(MessageBox.Show(this,"Allow Live Listen to ask about previously ignored apps again? Recording will still need your confirmation.","Reset ignored apps",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            listenPreferences.IgnoredApps.Clear();ignoredAppKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);SaveListenPreferences();listenState.Update(false,isRecording,false);DesktopDiagnostics.Record("preferences","local","ignored-apps-reset");
        }
        private void CheckRunningMeetings()
        {
            // Live Listen is opt-in; do not inspect other app windows when it is off.
            if(!liveListenEnabled)return;
            if(Interlocked.Exchange(ref detectorBusy,1)!=0)return;
            int generation=detectorGeneration;
            MeetingSignal signal;
            try { var ignored=ignoredAppKeys;signal=MeetingSignals.DetectDetailed(app=>ignored.Contains(app)); }
            catch { signal=new MeetingSignal{Reliable=false,Source="unavailable"}; }
            if(!IsHandleCreated||IsDisposed){Interlocked.Exchange(ref detectorBusy,0);return;}
            try{BeginInvoke(new Action(()=>{
              try{
                if(!isAuthenticated||!liveListenEnabled||detectorPromptOpen||generation!=detectorGeneration)return;
                latestMeetingSignal=signal;
                isMeetingDetected=!string.IsNullOrEmpty(signal.Title);
                activeMeetingTitle=signal.Title??"No active call detected";
                DesktopDiagnostics.Detection=isMeetingDetected?(signal.Source=="microphone"?"Other-app microphone activity":"Supported call controls"):signal.Reliable?"No active signal":"Evidence unavailable / incomplete";
                DesktopDiagnostics.Microphone=signal.Source=="microphone"?"Other app active (metadata only)":signal.Source=="call-controls"?"No confirmed other-app mic signal; call controls found":signal.Reliable?"No active other-app use observed":"Unknown";
                string detectionCode=signal.Source+"-"+(isMeetingDetected?"active":signal.Reliable?"idle":"unknown");
                if(detectionCode!=lastDetectionLog){lastDetectionLog=detectionCode;DesktopDiagnostics.Record("detection",signal.Source,isMeetingDetected?"active":signal.Reliable?"idle":"unknown");}
                lblRadarText.Text=isMeetingDetected?(signal.Source=="microphone"?"Microphone activity detected":"Call controls detected")+"\n"+signal.Title:signal.Reliable?"Live Listen on\nWaiting for microphone or call activity.":"Detection evidence unavailable. Record manually.";
                lblRadarText.ForeColor=isMeetingDetected?Color.FromArgb(150,220,190):Color.FromArgb(185,201,199);
                if(!signal.Reliable){listenState.DetectionUnavailable();return;}
                bool sameCall=isMeetingDetected&&(!isRecording||capturedSignalKey==signal.Key);
                string action=listenState.Update(sameCall,isRecording,liveListen!=null&&liveListen.Checked,signal.Key);
                if(action=="confirm-start"&&btnRecord.Enabled){
                    detectorPromptOpen=true;
                    try{
                        string answer=ShowDetectedMeetingPrompt(signal);
                        DesktopDiagnostics.Record("prompt","local",answer);
                        if(answer=="ignore-app"&&signal.AppKey!=null){listenPreferences.Ignore(signal.AppKey);ignoredAppKeys=new HashSet<string>(listenPreferences.IgnoredApps,StringComparer.OrdinalIgnoreCase);SaveListenPreferences();}
                        if(answer=="start"&&isAuthenticated&&liveListen.Checked&&!isRecording&&btnRecord.Enabled){
                            recordingFromDetection=true;
                            try{BtnRecord_Click(btnRecord,EventArgs.Empty);}finally{recordingFromDetection=false;}
                        }
                    }finally{detectorPromptOpen=false;}
                }
                else if(action=="confirm-stop"&&isRecording) {
                    detectorPromptOpen=true;
                    try{
                        var answer=MessageBox.Show("The detected call controls have been absent for at least 24 seconds. Stop recording and prepare your notes? Choose No to keep recording.","Briefly recording",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2);
                        if(answer==DialogResult.Yes&&isRecording)BtnRecord_Click(btnRecord,EventArgs.Empty);
                    }finally{detectorPromptOpen=false;}
                }
              }finally{Interlocked.Exchange(ref detectorBusy,0);}
            }));}catch(InvalidOperationException){Interlocked.Exchange(ref detectorBusy,0);}
        }

        private void UpdateRadarUI(string head, string sub, Color color)
        {
            if (this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(() =>
                {
                    lblRadarText.Text = head + "\n" + sub;
                    lblRadarText.ForeColor = color;
                    picRadarDot.Invalidate();
                }));
            }
        }
    }
}
