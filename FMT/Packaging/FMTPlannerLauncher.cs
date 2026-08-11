using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace FMTPlannerPortable
{
    internal static class Program
    {
        private const string PayloadResource = "FMTPlanner.Payload.zip";
        private const string ApplicationExecutable = "MissionPlanner.exe";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                string applicationDirectory = EnsureApplicationFiles();
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(applicationDirectory, ApplicationExecutable),
                    WorkingDirectory = applicationDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "FMTPlanner could not be started.\r\n\r\n" + exception.Message,
                    "FMTPlanner",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string EnsureApplicationFiles()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string payloadHash;

            using (Stream payload = assembly.GetManifestResourceStream(PayloadResource))
            {
                if (payload == null)
                    throw new InvalidOperationException("The embedded application package is missing.");

                using (SHA256 hashAlgorithm = SHA256.Create())
                    payloadHash = ToHex(hashAlgorithm.ComputeHash(payload)).Substring(0, 16);
            }

            string applicationRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FMT",
                "FMTPlanner");
            string destination = Path.Combine(applicationRoot, payloadHash);
            string marker = Path.Combine(destination, ".complete");
            string executable = Path.Combine(destination, ApplicationExecutable);

            if (File.Exists(marker) && File.Exists(executable))
                return destination;

            Directory.CreateDirectory(applicationRoot);
            string temporaryDestination = destination + ".tmp-" + Guid.NewGuid().ToString("N");

            try
            {
                Directory.CreateDirectory(temporaryDestination);
                ExtractPayload(assembly, temporaryDestination);

                if (!File.Exists(Path.Combine(temporaryDestination, ApplicationExecutable)))
                    throw new InvalidDataException("The application executable is missing from the package.");

                File.WriteAllText(
                    Path.Combine(temporaryDestination, ".complete"),
                    payloadHash,
                    Encoding.ASCII);

                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);

                Directory.Move(temporaryDestination, destination);
            }
            finally
            {
                if (Directory.Exists(temporaryDestination))
                    Directory.Delete(temporaryDestination, true);
            }

            return destination;
        }

        private static void ExtractPayload(Assembly assembly, string destination)
        {
            string destinationRoot = Path.GetFullPath(destination)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using (Stream payload = assembly.GetManifestResourceStream(PayloadResource))
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string outputPath = Path.GetFullPath(Path.Combine(destination, relativePath));

                    if (!outputPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The application package contains an invalid path.");

                    if (String.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(outputPath);
                        continue;
                    }

                    string parentDirectory = Path.GetDirectoryName(outputPath);
                    if (!String.IsNullOrEmpty(parentDirectory))
                        Directory.CreateDirectory(parentDirectory);

                    using (Stream source = entry.Open())
                    using (var target = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        source.CopyTo(target);

                    File.SetLastWriteTime(outputPath, entry.LastWriteTime.LocalDateTime);
                }
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }
}
