using System;
using System.Net;
using System.Windows;
using System.Windows.Threading;

namespace PipelineGenWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ConfigureNetworkDefaults();
            base.OnStartup(e);
        }

        public static void ConfigureNetworkDefaults()
        {
            try
            {
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 24;
                try
                {
                    // Enable TLS 1.2 + TLS 1.3 where supported by Windows
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
                }
                catch
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }
            }
            catch
            {
            }
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Непредвиденная ошибка:\n\n" + e.Exception.Message,
                "PIP — PipelineGen",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
