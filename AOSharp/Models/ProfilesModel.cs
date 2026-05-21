using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Collections.ObjectModel;

namespace AOSharp.Models
{
    public class ProfilesModel
    {
        public ObservableCollection<Profile> Profiles { get; set; }

        private readonly DispatcherTimer _timer;

        public ProfilesModel(Config config)
        {
            Profiles = new ObservableCollection<Profile>(config.Profiles);
            RefreshProfiles(null, null);

            _timer = new DispatcherTimer();
            _timer.Tick += new EventHandler(RefreshProfiles);
            _timer.Interval = new TimeSpan(0, 0, 1);
            _timer.Start();
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Find all visible "Anarchy Online - CharacterName" windows and return
        // a map of profile-name-suffix -> owning Process (Clientd.exe).
        private Dictionary<string, Process> FindAOWindows()
        {
            var results = new Dictionary<string, Process>();

            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                var sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string title = sb.ToString();

                string[] parts = title.Split(new char[] { '-' }, 2);
                if (parts.Length < 2 || !parts[0].TrimEnd().Equals("Anarchy Online", StringComparison.OrdinalIgnoreCase))
                    return true;

                GetWindowThreadProcessId(hwnd, out uint pid);
                try
                {
                    Process proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.Equals("Clientd", StringComparison.OrdinalIgnoreCase) ||
                        proc.ProcessName.Equals("AnarchyOnline", StringComparison.OrdinalIgnoreCase))
                    {
                        results[parts[1]] = proc;
                    }
                }
                catch { }

                return true;
            }, IntPtr.Zero);

            return results;
        }

        private void RefreshProfiles(object sender, EventArgs e)
        {
            foreach (Profile profile in Profiles)
                profile.IsActive = false;

            Dictionary<string, Process> aoWindows = FindAOWindows();

            foreach (var kvp in aoWindows)
            {
                string nameSuffix = kvp.Key;   // e.g. " Elyrah"
                Process proc = kvp.Value;

                Profile profile = Profiles.FirstOrDefault(x => x.Name == nameSuffix);

                if (profile == null)
                {
                    profile = new Profile() { Name = nameSuffix };
                    Profiles.Add(profile);
                }

                profile.IsActive = true;
                profile.Process = proc;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
