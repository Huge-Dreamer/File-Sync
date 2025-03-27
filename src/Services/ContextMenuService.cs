using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FlashSync.Services
{
    public class ContextMenuService
    {
        private const string AppName = "FlashSync";
        private const string ContextMenuName = "Sync with FlashSync";
        
        // Registry keys
        private const string DirectoryContextMenuKey = @"Directory\shell\FlashSync";
        private const string DirectoryContextMenuCommandKey = @"Directory\shell\FlashSync\command";
        private const string DriveContextMenuKey = @"Drive\shell\FlashSync";
        private const string DriveContextMenuCommandKey = @"Drive\shell\FlashSync\command";
        
        public bool IsContextMenuEnabled()
        {
            using (var key = Registry.ClassesRoot.OpenSubKey(DirectoryContextMenuKey))
            {
                return key != null;
            }
        }
        
        public bool AddContextMenu(string executablePath)
        {
            try
            {
                // Add context menu for directories
                using (var key = Registry.ClassesRoot.CreateSubKey(DirectoryContextMenuKey))
                {
                    if (key != null)
                    {
                        key.SetValue("", ContextMenuName);
                        key.SetValue("Icon", $"\"{executablePath}\"");
                    }
                }
                
                using (var key = Registry.ClassesRoot.CreateSubKey(DirectoryContextMenuCommandKey))
                {
                    if (key != null)
                    {
                        key.SetValue("", $"\"{executablePath}\" \"%1\"");
                    }
                }
                
                // Add context menu for drives
                using (var key = Registry.ClassesRoot.CreateSubKey(DriveContextMenuKey))
                {
                    if (key != null)
                    {
                        key.SetValue("", ContextMenuName);
                        key.SetValue("Icon", $"\"{executablePath}\"");
                    }
                }
                
                using (var key = Registry.ClassesRoot.CreateSubKey(DriveContextMenuCommandKey))
                {
                    if (key != null)
                    {
                        key.SetValue("", $"\"{executablePath}\" \"%1\"");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding context menu: {ex.Message}");
                return false;
            }
        }
        
        public bool RemoveContextMenu()
        {
            try
            {
                // Remove directory context menu
                Registry.ClassesRoot.DeleteSubKeyTree(DirectoryContextMenuKey, false);
                
                // Remove drive context menu
                Registry.ClassesRoot.DeleteSubKeyTree(DriveContextMenuKey, false);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing context menu: {ex.Message}");
                return false;
            }
        }
        
        public static bool IsAdministrator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            
            return false;
        }
        
        public static bool RestartAsAdmin(string[] args)
        {
            if (!IsAdministrator())
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        Verb = "runas"
                    };
                    
                    if (args.Length > 0)
                    {
                        startInfo.Arguments = string.Join(" ", args);
                    }
                    
                    Process.Start(startInfo);
                    return true; // Successfully started the process as admin
                }
                catch
                {
                    return false; // User denied elevation
                }
            }
            
            return false; // Already running as admin
        }
    }
} 