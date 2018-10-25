using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;

namespace HttpCache
{
    public partial class MainWindow : Window
    {
        private bool _isConfigurationDirty = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            UpdateButtons();
        }

        private void LoadConfiguration()
        {
            var isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null);

            string text = string.Empty;
            if (isoStore.FileExists("configuration.txt"))
            {
                using (var isoStream = new IsolatedStorageFileStream("configuration.txt", FileMode.Open, isoStore))
                using (var reader = new StreamReader(isoStream))
                {
                    text = reader.ReadToEnd();
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                TbxRules.Text = text;
            }
            else
            {
                TbxRules.Text = DefaultConfigurationString();
            }

            _isConfigurationDirty = false;
        }

        private void SaveConfiguration(string configuration)
        {
            var isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null);
            using (var isoStream = new IsolatedStorageFileStream("configuration.txt", FileMode.Create, isoStore))
            using (var writer = new StreamWriter(isoStream))
            {
                writer.Write(configuration);
            }

            _isConfigurationDirty = false;
        }

        private void TbxRules_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _isConfigurationDirty = true;
            UpdateButtons();
        }

        private static string DefaultConfigurationString()
        {
            return @"# Define the list of rules (one by line)
# A rule is composed of a http method or * followed by a regex that is applied on the url
# Exemples:
# GET https://www.google.com/.*
# POST https://www.google.com/.*
# * https://www.google.com/.*";
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            CacheProxy.ClearCache();
            MessageBox.Show("Cache cleared");
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            CacheProxy.SetConfiguration(TbxRules.Text);
            CacheProxy.Start();
            UpdateButtons();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            CacheProxy.Stop();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool isEnabled = CacheProxy.IsStarted();
            BtnStart.IsEnabled = !isEnabled;
            BtnStop.IsEnabled = isEnabled;

            BtnSaveConfiguration.IsEnabled = _isConfigurationDirty;
        }

        private void BtnSaveConfiguration_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration(TbxRules.Text);

            try
            {
                CacheProxy.SetConfiguration(TbxRules.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The configuration is not valid. " + ex.Message);
            }

            UpdateButtons();
        }
    }
}
