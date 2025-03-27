using System;
using System.Windows;
using FlashSync.Services;
using FlashSync.UI;
using System.IO;

namespace FlashSync
{
    public partial class App : Application
    {
        public App()
        {
            // Add handler for unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            string errorMessage = exception?.ToString() ?? "Unknown error occurred";
            
            ShowErrorMessage(errorMessage);
            
            // Log error to file
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "File-Sync", "crash_log.txt");
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"[{DateTime.Now}] {errorMessage}{Environment.NewLine}");
            }
            catch
            {
                // Failed to write to log file
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowErrorMessage(e.Exception.ToString());
            e.Handled = true;
        }

        private void ShowErrorMessage(string message)
        {
            try
            {
                MessageBox.Show($"An unexpected error occurred:\n\n{message}\n\nPlease check crash_log.txt in the application data folder.", 
                    "File-Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Could not show message box
            }
        }

        protected void Application_Startup(object sender, StartupEventArgs e)
        {
            // Check if the application needs to run with administrative privileges for context menu integration
            if (e.Args.Length > 0 && e.Args[0] == "--integrate")
            {
                if (!FlashSync.Services.ContextMenuService.IsAdministrator())
                {
                    bool restarted = FlashSync.Services.ContextMenuService.RestartAsAdmin(new string[] { "--integrate" });
                    if (restarted)
                    {
                        Shutdown();
                        return;
                    }
                }
                else
                {
                    // We're running as admin now, add context menu integration
                    var contextMenuService = new FlashSync.Services.ContextMenuService();
                    bool success = contextMenuService.AddContextMenu(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    
                    MessageBox.Show(success ? 
                        "Context menu integration added successfully." : 
                        "Failed to add context menu integration.", 
                        "Context Menu Integration", 
                        MessageBoxButton.OK, 
                        success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    
                    Shutdown();
                    return;
                }
            }
            
            // Start the main window
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
} 