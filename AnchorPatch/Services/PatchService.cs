using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace AnchorPatch.Services
{
    public sealed partial class PatchService
    {
        [LibraryImport("user32.dll", EntryPoint = "SetWindowTextW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowText(IntPtr hWnd, string lpString);

        Process[] processes;
        const string PROCESS_NAME = "Anchor Wallet";
        readonly int maxProcesses = 5;
        readonly Dictionary<int, Process> anchorProcesses = new();
        readonly ServiceLogger logger;
        bool isApplicationRunning;

        public PatchService(IHostApplicationLifetime hostApplication) 
        {
            hostApplication.ApplicationStopping.Register(OnApplicationStopping);
            logger = new ServiceLogger();
            isApplicationRunning = true;
        }

        void OnApplicationStopping()
        {
            isApplicationRunning = false;
        }

        void StartWatching()
        {
            while (isApplicationRunning)
            {
                processes = Process.GetProcessesByName(PROCESS_NAME);
                if (processes == null)
                {
                    Task.Delay(500).Wait();
                    continue;
                }
                RemoveExitedProcesses();
                if (anchorProcesses.Count > 0 && !processes.All(x => anchorProcesses.ContainsKey(x.Id)) && processes.Length > maxProcesses)
                {
                    KillZombieProcesses();
                }
                if (!UpdateMainWindowTitleIfNeeded())
                {
                    Task.Delay(500).Wait();
                    continue;
                }
                AddActiveProcesses();
                EnableExitedEventIfNotEnabled();
                Task.Delay(1000).Wait();
            }
        }

        void RemoveExitedProcesses()
        {
            anchorProcesses.Keys.Where(id => anchorProcesses[id].HasExited).ToList().ForEach(p =>
            {
                if (anchorProcesses.Remove(p))
                {
                    logger.LogInformation($"Removed exited Anchor process: {p}");
                }
            });
        }

        void KillZombieProcesses()
        {
            foreach (var zombieProcess in processes)
            {
                if (!IsParentProcessExist(GetParentProcessId(zombieProcess.Id)))
                {
                    zombieProcess.Kill(true);
                    logger.LogInformation($"Killed Zombie Process Id: {zombieProcess.Id}");
                }
            }
        }

        bool IsParentProcessExist(int processId)
        {
            if (processId == -1) return false;
            return Process.GetProcesses().Any(x => x.Id == processId);
        }

        int GetParentProcessId(int processId)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using ManagementObjectSearcher searcher = new($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            using ManagementObjectCollection objects = searcher.Get();
            foreach (ManagementObject obj in objects.Cast<ManagementObject>())
            {

                object parentId = obj["ParentProcessId"];
                if (parentId != null)
                {
                    return Convert.ToInt32(parentId);
                }
            }
#pragma warning restore CA1416 // Validate platform compatibility

            logger.LogInformation($"Failed to retrieve parent process ID for process {processId}, => Zombie detected");
            return -1;
        }

        bool UpdateMainWindowTitleIfNeeded()
        {
            if (processes.Any(x => x.MainWindowHandle != IntPtr.Zero))
            {
                var mainWindow = processes.FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
                if (mainWindow.MainWindowTitle.StartsWith("Anchor") && !mainWindow.MainWindowTitle.Contains("[Patched]"))
                {
                    SetWindowText(mainWindow.MainWindowHandle, $"{mainWindow.MainWindowTitle} [Patched]");
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        void AddActiveProcesses()
        {
            foreach (var anchorProcess in processes.Where(x => !x.HasExited))
            {
                if(anchorProcesses.Count != maxProcesses)
                    anchorProcesses.TryAdd(anchorProcess.Id, anchorProcess);
            }
        }

        void EnableExitedEventIfNotEnabled()
        {
            if (anchorProcesses.Count > 0 && !anchorProcesses.Values.First().EnableRaisingEvents)
            {
                anchorProcesses.Values.First().EnableRaisingEvents = true;
                anchorProcesses.Values.First().Exited += AnchorProcess_Exited;
            }
        }

        void AnchorProcess_Exited(object sender, EventArgs e)
        {
            foreach (var anchorProcess in Process.GetProcessesByName(PROCESS_NAME))
            {
                anchorProcess.Close();
                if (!anchorProcess.WaitForExit(TimeSpan.FromSeconds(2)))
                {
                    anchorProcess.Kill(true);
                }
            }
            anchorProcesses.Clear();
            logger.LogInformation("All Anchor process exit.");
        }

        public void Start()
        {
            logger.LogInformation("StartWatching");
            Task.Run(StartWatching);
        }
    }
}
