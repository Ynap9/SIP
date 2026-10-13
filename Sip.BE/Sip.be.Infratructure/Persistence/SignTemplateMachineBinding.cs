using Microsoft.Win32;
using Sip.be.Shared.Constants.Documents;
using System.Security.Cryptography;
using System.Text;

namespace Sip.be.infratructure.Persistence
{
    public static class SignTemplateMachineBinding
    {
        public static bool Ensure()
        {
            Directory.CreateDirectory(SignTemplateConstants.AppDataDirectory);

            var machineId = Environment.MachineName;
            if (OperatingSystem.IsWindows())
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = hklm.OpenSubKey(SignTemplateConstants.MachineGuidRegistryPath);
                if (key?.GetValue(SignTemplateConstants.MachineGuidValueName) is string guid
                    && !string.IsNullOrWhiteSpace(guid))
                {
                    machineId = guid;
                }
            }

            var current = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(SignTemplateConstants.AppDataFolderName + "|" + machineId)));
            var bindingPath = SignTemplateConstants.MachineBindingPath;

            if (!File.Exists(bindingPath))
            {
                File.WriteAllText(bindingPath, current);
                return false;
            }

            if (string.Equals(File.ReadAllText(bindingPath).Trim(), current, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var databasePath = SignTemplateConstants.DatabasePath;
            foreach (var file in new[] { databasePath, databasePath + "-wal", databasePath + "-shm", databasePath + "-journal" })
            {
                if (File.Exists(file)) File.Delete(file);
            }
            if (Directory.Exists(SignTemplateConstants.AssetDirectory))
            {
                Directory.Delete(SignTemplateConstants.AssetDirectory, recursive: true);
            }

            File.WriteAllText(bindingPath, current);
            return true;
        }
    }
}
