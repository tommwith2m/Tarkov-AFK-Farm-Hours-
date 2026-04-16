using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TarkovAF
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);

        private AppConfig _config = new AppConfig();
        private DispatcherTimer _timer;
        private DateTime _sessionStart;
        private DateTime? _nextAFK;
        private bool _isMonitoring = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => {
                if (!_isMonitoring) return;
                var proc = Process.GetProcessesByName("EscapeFromTarkov").FirstOrDefault();
                if (proc != null)
                {
                    txtStatus.Text = "STATUS: ACTIVE";
                    if (_nextAFK == null) _nextAFK = DateTime.Now.AddSeconds(new Random().Next(180, 420));
                    txtNextAFK.Text = $"Next AFK: {(_nextAFK.Value - DateTime.Now):mm\\:ss}";
                    if (DateTime.Now >= _nextAFK) DoAntiAFK(proc.MainWindowHandle);
                }
                else { txtStatus.Text = "STATUS: SEARCHING..."; }
                txtUptime.Text = $"Session: {(DateTime.Now - _sessionStart):hh\\:mm\\:ss}";
            };
            _timer.Start();
        }

        private void DoAntiAFK(IntPtr handle)
        {
            try
            {
                ShowWindow(handle, 9); // Restore window
                SetForegroundWindow(handle);
                System.Threading.Thread.Sleep(500);

                System.Windows.Forms.SendKeys.SendWait("{TAB}");
                System.Threading.Thread.Sleep(300);
                System.Windows.Forms.SendKeys.SendWait("{ESC}");

                AppendLog("Anti-AFK Sent");
                _nextAFK = DateTime.Now.AddSeconds(new Random().Next(180, 420));
            }
            catch (Exception ex)
            {
                AppendLog("AFK Error: " + ex.Message);
            }
        }

        private void AppendLog(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }

        private void btnLauncher_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
            if (ofd.ShowDialog() == true)
            {
                _config.LauncherPath = ofd.FileName;
                txtLauncherPath.Text = Path.GetFileName(ofd.FileName);
                SaveSettings();
                AppendLog($"Launcher set to: {txtLauncherPath.Text}");
            }
        }

        private void btnStart_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.LauncherPath)) { System.Windows.MessageBox.Show("Set Launcher!"); return; }
            _isMonitoring = true; _sessionStart = DateTime.Now;
            btnStart.IsEnabled = false; btnStop.IsEnabled = true;
            AppendLog("Monitoring started.");
        }

        private void btnStop_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _isMonitoring = false; btnStart.IsEnabled = true; btnStop.IsEnabled = false;
            AppendLog("Monitoring stopped.");
        }

        private async void btnCapture_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            btnCapture.Content = "HOLD 'P'...";
            await Task.Run(() => {
                while ((GetAsyncKeyState(0x50) & 0x8000) == 0) System.Threading.Thread.Sleep(50);
                GetCursorPos(out POINT p);
                _config.PlayX = p.X; _config.PlayY = p.Y;
            });
            btnCapture.Content = "Saved!"; SaveSettings();
            AppendLog("Click coordinates captured.");
        }

        private void btnAFKTest_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var proc = Process.GetProcessesByName("EscapeFromTarkov").FirstOrDefault();
            if (proc != null) DoAntiAFK(proc.MainWindowHandle);
            else AppendLog("Test Failed: Game not found.");
        }

        private void SaveSettings() => File.WriteAllText("settings.json", JsonSerializer.Serialize(_config));
        private void LoadSettings()
        {
            if (File.Exists("settings.json")) _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText("settings.json"));
            txtLauncherPath.Text = string.IsNullOrEmpty(_config.LauncherPath) ? "No path set" : Path.GetFileName(_config.LauncherPath);
        }

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    }

    public class AppConfig { public string LauncherPath { get; set; } = ""; public int PlayX { get; set; } public int PlayY { get; set; } }
}