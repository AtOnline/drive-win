using DokanNet;
using Drive.Atonline;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

namespace Drive
{
    /// <summary>
    /// Interaction logic for Mount.xaml
    /// </summary>
    public partial class Mounter : Page
    {
        public ObservableCollection<Atonline.Rest.Drive> AvailableDrives { get; set; }
        public Dictionary<char, Atonline.VirtualDrive> DriveToMount = new Dictionary<char, Atonline.VirtualDrive>();

        public Mounter()
        {
            InitializeComponent();
            DataContext = this;
            AvailableDrives = new ObservableCollection<Atonline.Rest.Drive>();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }


        private void setDefaultStatus(ListViewItem lsi, char letter)
        {
            var myContentPresenter = FindVisualChild<ContentPresenter>(lsi);
            var myDataTemplate = myContentPresenter.ContentTemplate;
            var cb = (ComboBox)myDataTemplate.FindName("DriveLetter", myContentPresenter);

            var lst = getAvailableDriveLetters();
            lst.Add(letter);
            cb.ItemsSource = lst;
            cb.SelectedItem = letter;

            var unMountBtn = (Button)myDataTemplate.FindName("UnmountBtn", myContentPresenter);
            unMountBtn.Visibility = Visibility.Visible;
        }

        private void listViewStatusChanged(object s, EventArgs ev)
            {

            //TODO clean this stuff (used to load the mounted drive settings and check the checkboxes and set the combobox value)
                var ok = (s as ItemContainerGenerator).Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated;
                if(ok)
                {
                    var defaultSettings = Settings.ExtractSettingDriveInfos();

                    foreach(var d in AvailableDrives)
                    {
                      ListViewItem lsi = (ListViewItem)DriveList.ItemContainerGenerator.ContainerFromItem(d);
                        if (defaultSettings.ContainsKey(((Atonline.Rest.Drive) lsi.DataContext).Drive__))
                        {
                            var driveResp  =(Atonline.Rest.Drive)lsi.DataContext;
                             char letter = defaultSettings[driveResp.Drive__];
                        setDefaultStatus(lsi, defaultSettings[driveResp.Drive__]);
                        }
                    }

                   DriveList.ItemContainerGenerator.StatusChanged -= listViewStatusChanged;
                }
            }

        private async void Page_Initialized(object sender, EventArgs e)
        {
            var r = await RestClient.Drives();
            r.ToList().ForEach(AvailableDrives.Add);

            DriveList.ItemContainerGenerator.StatusChanged += listViewStatusChanged;

            Loader.Visibility = Visibility.Hidden;
            DriveGrid.Visibility = Visibility.Visible;
        }

        public List<char> getAvailableDriveLetters()
        {
            return getAvailableDriveLetters(null);
        }

        public List<char> getAvailableDriveLetters(char? force)
        {
            List<char> driveLetters = new List<char>();
            for (int i = 65; i < 91; i++) // increment from ASCII values for A-Z
            {
                driveLetters.Add(Convert.ToChar(i)); // Add uppercase letters to possible drive letters
            }

            foreach (string drive in Directory.GetLogicalDrives())
            {
                if (force != null && (drive[0] == force)) continue;
                driveLetters.Remove(drive[0]); // removed used drive letters from possible drive letters
            }

            foreach (var kv in DriveToMount)
            {
                if (force != null && (kv.Value.DriveLetter == force)) continue;
                driveLetters.Remove(kv.Value.DriveLetter);
            }

            return driveLetters;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var curItem = ((ListViewItem)DriveList.ContainerFromElement((CheckBox)sender));
            var myContentPresenter = FindVisualChild<ContentPresenter>(curItem);
            var myDataTemplate = myContentPresenter.ContentTemplate;
            var cb = (ComboBox)myDataTemplate.FindName("DriveLetter", myContentPresenter);
            cb.Visibility = Visibility.Visible;

            cb.ItemsSource = getAvailableDriveLetters();
            cb.SelectedIndex = cb.Items.Count - 1;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var curItem = ((ListViewItem)DriveList.ContainerFromElement((CheckBox)sender));
            var myContentPresenter = FindVisualChild<ContentPresenter>(curItem);
            var myDataTemplate = myContentPresenter.ContentTemplate;
            var cb = (ComboBox)myDataTemplate.FindName("DriveLetter", myContentPresenter);
            cb.Visibility = Visibility.Hidden;
            var selectedItem = cb.SelectedItem as ComboBoxItem;
            cb.SelectedIndex = -1;
            if (selectedItem == null) return;
            DriveToMount.Remove((char)selectedItem.Content);
        }

        private void DriveLetter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (e!= null && e.RemovedItems.Count > 0)
            {
                char c = (char)(e.RemovedItems[0]);
                ((App)App.Current).Unmount(c, true);
            }

            var sI = (sender as ComboBox).SelectedItem;
            if (sI == null) return;

            var selectedLetter = (char)sI;
            if (!DriveToMount.ContainsKey(selectedLetter))
            {
                var curItem = ((ListViewItem)DriveList.ContainerFromElement((sender as ComboBox)));
                var myContentPresenter = FindVisualChild<ContentPresenter>(curItem);
                var myDataTemplate = myContentPresenter.ContentTemplate;
                ((Button)myDataTemplate.FindName("UnmountBtn", myContentPresenter)).Visibility = Visibility.Visible;
                ((App)App.Current).Mount(selectedLetter, (sender as ComboBox).DataContext as Atonline.Rest.Drive, true);
            }

        }


        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private void DriveLetter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            List<char> lts;
            var old = (sender as ComboBox).SelectedItem;
            if ((sender as ComboBox).SelectedIndex >=0)
            {
                lts = getAvailableDriveLetters((char)(sender as ComboBox).SelectedItem);
            }else
            {
                lts = getAvailableDriveLetters();
            }

            (sender as ComboBox).ItemsSource = lts;
        }

        private void UnmountBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            btn.Visibility = Visibility.Hidden;

            var curItem = ((ListViewItem)DriveList.ContainerFromElement(btn));
            var myContentPresenter = FindVisualChild<ContentPresenter>(curItem);
            var myDataTemplate = myContentPresenter.ContentTemplate;
            var cb = (ComboBox)myDataTemplate.FindName("DriveLetter", myContentPresenter);
            cb.SelectedIndex = -1;
        }
    }
}
