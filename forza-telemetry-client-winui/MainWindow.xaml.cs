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
using Windows.Storage.Pickers;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace forza_telemetry_client_winui
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        AppWindow m_appWindow;
        //private DispatcherTimer raceOffTimer;
        private int _fPressCount = 0;

        public MainWindow()
        {
            this.InitializeComponent();
            viewModel = new MainViewModel();

            viewModel.SessionOverEvent += OnSessionOver;

            m_appWindow = GetAppWindowForCurrentWindow();
            m_appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

            // Listen for key presses anywhere in the window (handled events too)
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootGridKeyDown), true);
            
            // Set default mode to Live UDP
            ModeComboBox.SelectedIndex = 0;
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

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                string mode = selectedItem.Tag?.ToString();
                
                if (mode == "Dump" || mode == "Replay")
                {
                    FilePathPanel.Visibility = Visibility.Visible;
                    DumpReplayControls.Visibility = Visibility.Visible;
                    
                    // Update default file name based on mode
                    if (mode == "Dump")
                    {
                        FilePathTextBox.Text = $"telemetry_dump_{DateTime.Now:yyyyMMdd_HHmmss}.fbs";
                    }
                    else if (mode == "Replay")
                    {
                        FilePathTextBox.Text = "telemetry_replay.fbs";
                    }
                }
                else
                {
                    FilePathPanel.Visibility = Visibility.Collapsed;
                    DumpReplayControls.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mode = ((ComboBoxItem)ModeComboBox.SelectedItem)?.Tag?.ToString();
                
                if (mode == "Dump")
                {
                    // File save picker for dump mode
                    var savePicker = new FileSavePicker();
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                    
                    savePicker.SuggestedFileName = $"telemetry_dump_{DateTime.Now:yyyyMMdd_HHmmss}";
                    savePicker.FileTypeChoices.Add("FlatBuffer Files", new List<string>() { ".fbs" });
                    savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    
                    var file = await savePicker.PickSaveFileAsync();
                    if (file != null)
                    {
                        FilePathTextBox.Text = file.Path;
                    }
                }
                else if (mode == "Replay")
                {
                    // File open picker for replay mode
                    var openPicker = new FileOpenPicker();
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
                    
                    openPicker.FileTypeFilter.Add(".fbs");
                    openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    
                    var file = await openPicker.PickSingleFileAsync();
                    if (file != null)
                    {
                        FilePathTextBox.Text = file.Path;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error browsing files: {ex.Message}");
                
                // Show error dialog
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Unable to browse files: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mode = ((ComboBoxItem)ModeComboBox.SelectedItem)?.Tag?.ToString();
                var filePath = FilePathTextBox.Text;
                
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please specify a file path.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }
                
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                ModeComboBox.IsEnabled = false;
                
                System.Diagnostics.Debug.WriteLine($"Starting {mode} mode with file: {filePath}");
                
                if (mode == "Dump")
                {
                    await viewModel.StartDumpModeAsync(filePath);
                }
                else if (mode == "Replay")
                {
                    await viewModel.StartReplayModeAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting operation: {ex.Message}");
                
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ModeComboBox.IsEnabled = true;
                
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Unable to start operation: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mode = ((ComboBoxItem)ModeComboBox.SelectedItem)?.Tag?.ToString();
                
                System.Diagnostics.Debug.WriteLine($"Stopping {mode} mode");
                
                if (mode == "Dump")
                {
                    await viewModel.StopDumpAsync();
                }
                else if (mode == "Replay")
                {
                    viewModel.StopReplay();
                }
                
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ModeComboBox.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping operation: {ex.Message}");
                
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Unable to stop operation: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

    }
}