using System;
using System.IO;
using Microsoft.Win32;

namespace Spacio
{
    // Shared by Setup and the application. Portable copies must never alter an
    // installation's registration; only its exact registered executable may do so.
    public static class InstallationRegistration
    {
        public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Spacio";
        public static void Refresh(string executable, Version version)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                using (var key = root.OpenSubKey(KeyPath, true)) RefreshKey(key, executable, version);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (System.Security.SecurityException) { } catch (ArgumentException) { } catch (NotSupportedException) { }
        }
        public static bool RefreshKey(RegistryKey key, string executable, Version version)
        {
            if (key == null) return false;
            string installed = Convert.ToString(key.GetValue("InstallLocation"));
            if (String.IsNullOrWhiteSpace(installed)) return false;
            if (!String.Equals(Path.GetFullPath(executable), Path.GetFullPath(Path.Combine(installed, "Spacio.exe")), StringComparison.OrdinalIgnoreCase)) return false;
            key.SetValue("DisplayVersion", version.ToString(3)); return true;
        }
    }
}
