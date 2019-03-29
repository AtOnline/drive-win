using Drive.Atonline;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static Drive.App;

namespace Drive
{
    /// <summary>
    /// Interaction logic for LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
            Browser.Source = new Uri(Config.LoginUrl);
        }

        private void Browser_NavigationCompleted(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlNavigationCompletedEventArgs e)
        {
            Loader.Visibility = Visibility.Hidden;
            Browser.Visibility = Visibility.Visible;
        }

        private async void Browser_NavigationStarting(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlNavigationStartingEventArgs e)
        {

            if (e.Uri.Host == Config.REDIRECT_HOST && e.Uri.AbsolutePath == Config.REDIRECT_ACTION)
            {
                Loader.Visibility = Visibility.Visible;
                Browser.Visibility = Visibility.Hidden;
             
               //e.Cancel = true;

                var code = System.Web.HttpUtility.ParseQueryString(e.Uri.Query).Get("code");
                var p = new Dictionary<string, object>()
                {
                    { "grant_type","authorization_code" },
                    { "client_id",Config.CLIENT_ID },
                    { "redirect_uri",Config.REDIRECT_URI },
                    { "code",code },
                };

                var r = await RestClient.Post<Dictionary<string, object>>(Config.TOKEN_ENDPOINT, p);

                Config.ApiToken = new BaererToken((string)r["access_token"], (string)r["refresh_token"], DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)r["expires_in"]);
                (Application.Current as App)?.SetContextMenuStripStatus(ContextMenuStrip.Drive, true);
                (Application.Current as App) ?.SetContextMenuStripStatus(ContextMenuStrip.Logout, true);
                NavigationService.Navigate(new Uri("Mount.xaml", UriKind.Relative));
            }
        }
    }
}
