using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ForzaBridge.Model;
using ForzaBridge.ViewModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.System;
using System.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace forza_telemetry_client_winui
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        AppWindow m_appWindow;
        //private DispatcherTimer raceOffTimer;
        private int _fPressCount = 0;

        private string _statusMessage;
        public string StatusMessage 
        { 
            get => _statusMessage;
            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public MainWindow()
        {
            this.InitializeComponent();
            viewModel = new MainViewModel();

            viewModel.SessionOverEvent += OnSessionOver;

            m_appWindow = GetAppWindowForCurrentWindow();
            m_appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

            // Listen for key presses anywhere in the window (handled events too)
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootGridKeyDown), true);
            
            // Set status message based on telemetry mode
            var mode = forza_telemetry_client_winui.App.TelemetryMode;
            var filePath = forza_telemetry_client_winui.App.TelemetryFilePath;
            
            if (mode == "Dump")
            {
                StatusMessage = $"Telemetry Mode: Dump to File ({filePath})";
            }
            else if (mode == "Replay")
            {
                StatusMessage = $"Telemetry Mode: Replay from File ({filePath})";
            }
            else
            {
                StatusMessage = "Telemetry Mode: Live UDP";
            }
        }

        private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // F11 -> Full screen
            if (e.Key == VirtualKey.F11)
            {
                try
                {
                    m_appWindow?.SetPresenter(AppWindowPresenterKind.FullScreen);
                }
                catch { /* ignore presenter set failures */ }
                _fPressCount = 0;
                return;
            }

            // 10 consecutive 'f' -> Default presenter
            if (e.Key == VirtualKey.F)
            {
                _fPressCount++;
                if (_fPressCount >= 10)
                {
                    try
                    {
                        m_appWindow?.SetPresenter(AppWindowPresenterKind.Default);
                    }
                    catch { /* ignore presenter set failures */ }
                    _fPressCount = 0;
                }
            }
            else
            {
                _fPressCount = 0;
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

            return AppWindow.GetFromWindowId(myWndId);
        }


        public MainViewModel viewModel { get; set; }



        private async void SetSessionButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowSetSessionDialog();
        }

        public async Task ShowSetSessionDialog()
        {


            StackPanel dialogPanel = new StackPanel
            {
                //Background = new SolidColorBrush(Colors.Black)
            };

            TextBox userNameTextBox = new TextBox
            {
                PlaceholderText = "Name",
                Margin = new Thickness(0, 0, 0, 10),
            };

            TextBox emailTextBox = new TextBox
            {
                PlaceholderText = "Email",
                Margin = new Thickness(0, 0, 0, 10),
            };

            TextBox telephoneTextBox = new TextBox
            {
                PlaceholderText = "Telephone",
                Margin = new Thickness(0, 0, 0, 10),
            };

            TextBlock disclaimerText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Text = "Your privacy is important to us. The contact information you provide when submitting an entry to this Contest will be used to contact you throughout the Event with updates related to the Contest, potentially including ranking updates, daily winners, and tips for improving your racing time. If you choose to receive these updates via text message, you are responsible for any associated data charges from your wireless provider.\r\n \r\nWhen you opt in to the Contest, your racing data will be shared with the Microsoft Fabric Real-Time Intelligence demo environment at the Event and may be used in post-Event evaluation or in Microsoft marketing campaigns. However, your contact information will not be retained in connection with your racing data."
            };

            dialogPanel.Children.Add(userNameTextBox);
            dialogPanel.Children.Add(emailTextBox);
            dialogPanel.Children.Add(telephoneTextBox);
            dialogPanel.Children.Add(disclaimerText);



            ContentDialog inputDialog = new ContentDialog
            {
                Title = "Enter your session details",
                Content = dialogPanel,
                Width = 800,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                Background = (Brush)Application.Current.Resources["AcrylicBrushDark"],


                XamlRoot = this.Content.XamlRoot, // Set the XamlRoot property
            };

            Application.Current.Resources["ContentDialogTopOverlay"] = null;
            Application.Current.Resources["ContentDialogSeparatorBorderBrush"] = null;

            ContentDialogResult result = await inputDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                TextBox inputTextBox = inputDialog.Content as TextBox;

                string userNameInput = userNameTextBox.Text;
                string emailInput = emailTextBox.Text;
                string telephoneInput = telephoneTextBox.Text;

                // Create a new session from the user input
                Session session = new Session(userNameInput, emailInput, telephoneInput);

                // Update the view model with the new session
                viewModel.Session = session;

            }
        }

        private async void OnSessionOver(object sender, EventArgs e)
        {
            await ShowSetSessionDialog();

            //ContentDialog endSessionDialog = new ContentDialog
            //{
            //    Title = "Session Over",
            //    Content = "The race session has paused/ended.",
            //    PrimaryButtonText = "Create New Session",
            //    CloseButtonText = "Cancel",
            //    Background = (Brush)Application.Current.Resources["AcrylicBrushDark"],
            //    XamlRoot = this.Content.XamlRoot
            //};

            //ContentDialogResult result = await endSessionDialog.ShowAsync();

            //if (result == ContentDialogResult.Primary)
            //{
            //    await ShowSetSessionDialog();
            //}
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}