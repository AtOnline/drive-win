using DokanNet;
using Drive.Atonline;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Drive
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
   
        FileStream _fileLock;
        public Logger Logger { get; }

        public enum ContextMenuStrip
        {
            Drive = 0,
            Logout,
            Exit,
        }
        public App()
        {
            Logger = new Logger("App");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }


        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
           Logger.WriteLog(e.Exception.StackTrace + "\n" + e.Exception.Message + "\n" + e.Exception.InnerException?.StackTrace + "\n" + e.Exception.InnerException?.Message);

            e.Handled = true;
        }

        Dictionary<char, Atonline.VirtualDrive> _mountedDrives = new Dictionary<char, Atonline.VirtualDrive>();

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        public void UnmountFileSystems()
        {
            foreach (var kv in _mountedDrives)
            {
                kv.Value.Unmount();
            }

            _mountedDrives.Clear();
        }

        public void StoreMountedDriveSettings()
        {
            var s = "";
            foreach (var m in _mountedDrives)
            {
                s += $"{m.Value.DriveLetter}:{m.Value.Provider.Drive.Drive__}|";
            }
            Settings.Default.Drives = s;
            Settings.Default.Save();
        }

        public void Unmount(char c, bool saveSetting = false)
        {
            if (!_mountedDrives.ContainsKey(c)) return;
            _mountedDrives[c].Unmount();
            _mountedDrives.Remove(c);
            StoreMountedDriveSettings();
        }



        public void Mount(char c, Atonline.Rest.Drive dr, bool saveSetting = false)
        {
            if (_mountedDrives.ContainsKey(c)) return;
            var d = new Atonline.VirtualDrive(c, dr);
            _mountedDrives.Add(c, d);
            d.Mount();
            StoreMountedDriveSettings();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_fileLock != null)
            {
                _fileLock.Dispose();
                _fileLock.Close();
            }
            UnmountFileSystems();
            base.OnExit(e);
        }

        private bool checkProcess()
        {
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                path = Path.Combine(path, "AtOnline", "Drive");
                System.IO.Directory.CreateDirectory(path);
                _fileLock = new FileStream(path + "\\lock.lock", FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, FileShare.None);
            } catch (Exception e)
            {

                return false;
            }

            return true;
        }

        public void SetContextMenuStripStatus(ContextMenuStrip strip, bool status)
        {
            _notifyIcon.ContextMenuStrip.Items[(int)strip].Enabled = status;
        }

        protected async override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Resource.Culture = CultureInfo.CurrentCulture;

            if (!checkProcess())
            {
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((object sender, UnhandledExceptionEventArgs args) =>
            {
                Logger.WriteLog((args.ExceptionObject as Exception)?.StackTrace + "\n" + (args.ExceptionObject as Exception)?.Message + "\n" + (args.ExceptionObject as Exception)?.InnerException?.StackTrace + "\n" + (args.ExceptionObject as Exception)?.InnerException?.Message);
            });

            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(new Uri("Resource/icon.ico", UriKind.Relative).ToString());
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, ee) => ShowDrives();

            CreateContextMenu();

            SetContextMenuStripStatus(ContextMenuStrip.Logout, IsLogged);
            SetContextMenuStripStatus(ContextMenuStrip.Drive, IsLogged);

            if (!IsLogged)
            {
                DisplayWindow(WindowType.Welcome);
            }
            else if (!HasDriveConfigured)
            {
                DisplayWindow(WindowType.Drives);
            }
            else
            {
                var r = await RestClient.Drives();

                var defaultSettings = Settings.ExtractSettingDriveInfos();
                foreach (var d in r)
                {
                    if (defaultSettings.ContainsKey(d.Drive__))
                    {
                        char letter = defaultSettings[d.Drive__];

                        if (IdDriveUsed(letter))
                            throw new Exception("nope");
                        else
                            Mount(letter, d);
                    }
                }

            }
        }

        public bool IsLogged { get { return Config.ApiToken != null; } }
        public bool HasDriveConfigured { get { return Settings.Default.Drives != ""; } }


        private bool IdDriveUsed(char letter)
        {
            foreach (string drive in Directory.GetLogicalDrives())
            {
                if (drive[0] == letter) return true;
            }

            return false;
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add(Resource.tray_drives).Click += (s, e) => ShowDrives();
            _notifyIcon.ContextMenuStrip.Items.Add(Resource.tray_logout).Click += (s, e) => Logout();
            _notifyIcon.ContextMenuStrip.Items.Add(Resource.tray_exit).Click += (s, e) => ExitApplication();

            SetContextMenuStripStatus(ContextMenuStrip.Drive, false);
            SetContextMenuStripStatus(ContextMenuStrip.Logout, false);
        }

        private void ExitApplication()
        {
           StoreMountedDriveSettings();
           if (MainWindow!=null) MainWindow.Close();
            _notifyIcon.Dispose();
            _notifyIcon = null;
            Shutdown();
        }

        private void ShowDrives()
        {
            if (!IsLogged)
            {
                DisplayWindow(WindowType.Welcome);
                return;
            }

            if (MainWindow != null) MainWindow.Close();

            MainWindow = new MainWindow(new Mounter());
            MainWindow.Show();
        }

        private void Logout()
        {
            if (MainWindow != null) MainWindow.Close();
            Config.ApiToken = null;
            Settings.Default.Drives = "";
            Settings.Default.Save();
            UnmountFileSystems();
            SetContextMenuStripStatus(ContextMenuStrip.Drive, false);
            SetContextMenuStripStatus(ContextMenuStrip.Logout, false);

            MainWindow = new MainWindow(new WelcomePage());
            MainWindow.Show();
        }

        public enum WindowType
        {
            Welcome,
            Login,
            Drives,
        }

        public void DisplayWindow(WindowType type)
        {
            if (MainWindow != null)
            {
                MainWindow.Close();
            }
            Page p = null;
            switch (type)
            {
                case WindowType.Login:
                    p = new LoginPage();
                    break;
                case WindowType.Welcome:
                    p = new WelcomePage();
                    break;
                case WindowType.Drives:
                    p = new Mounter();
                    break;
            }

            MainWindow = new MainWindow(p);
            MainWindow.Show();
        }
    }
}
