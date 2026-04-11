using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using Windows.Media.Control;
using Microsoft.Toolkit.Uwp.Notifications;

namespace GameMediaDetection
{
    internal class Program
    {
        static HashSet<string> previousProcesses = new(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, DateTime> lastNotified = new();

        static Dictionary<string, List<string>> games = new()
        {
            { "Genshin Impact", new List<string> {
                "GenshinImpact.exe",
                "YuanShen.exe",
                "Genshin Impact Cloud.exe"
            }},
            { "Honkai: Star Rail", new List<string> {
                "Star Rail.exe"
            }},
            { "Zenless Zone Zero", new List<string> {
                "ZenlessZoneZero.exe",
                "Zenless Zone Zero Cloud.exe"
            }},
            { "Honkai Impact 3rd", new List<string> {
                "BH3.exe"
            }}
        };

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Tray icon
            NotifyIcon trayIcon = new NotifyIcon()
            {
                Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico")),
                Visible = true,
                Text = "GameMediaNotifier"
            };

            // Context menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon.ContextMenuStrip = menu;

            // Start background logic
            Task.Run(() => MonitorLoop());

            Application.Run();
        }

        static async Task MonitorLoop()
        {
            while (true)
            {
                var currentProcesses = GetRunningProcesses();
                var newProcesses = currentProcesses.Except(previousProcesses);

                foreach (var proc in newProcesses)
                {
                    foreach (var game in games)
                    {
                        if (game.Value.Any(x => x.Equals(proc, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (await IsMediaPlaying() && CanNotify(game.Key))
                            {
                                ShowNotification(game.Key);
                                lastNotified[game.Key] = DateTime.Now;
                            }
                        }
                    }
                }

                previousProcesses = currentProcesses;
                await Task.Delay(2000);
            }
        }

        static HashSet<string> GetRunningProcesses()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string name = Path.GetFileName(p.MainModule.FileName);
                    set.Add(name);
                }
                catch
                {
                    set.Add(p.ProcessName + ".exe");
                }
            }

            return set;
        }

        static async Task<bool> IsMediaPlaying()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();

            if (session == null) return false;

            var info = session.GetPlaybackInfo();
            return info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }

        static void ShowNotification(string gameName)
        {
            new ToastContentBuilder()
                .AddText("🎮 Game Detected")
                .AddText($"{gameName} launched while media is playing")
                .AddText("Consider switching your audio setup")
                .Show();
        }

        static bool CanNotify(string game)
        {
            if (!lastNotified.ContainsKey(game)) return true;

            return (DateTime.Now - lastNotified[game]).TotalMinutes > 5;
        }
    }
}