using System.Windows;

namespace HttpCache
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //CacheProxy.Start();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            CacheProxy.Stop();
        }
    }
}
