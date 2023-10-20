#pragma warning disable CA1416
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

        readonly ServiceLogger logger;
        readonly Dictionary<int, Process> anchorProcesses = new();
        const string PROCESS_NAME = "Anchor Wallet";
        Process[] processes;
        bool isApplicationRunning;

        public PatchService(IHostApplicationLifetime hostApplication) 
        {
            hostApplication.ApplicationStopping.Register(OnApplicationStopping);
            logger = new ServiceLogger();
            isApplicationRunning = true;
        }

        public void Start()
        {
            logger.LogInformation("Start Watching");
            Task.Run(StartWatching);
        }

        void StartWatching()
        {
            while (isApplicationRunning)
            {
                try
                {
                    SpinWait.SpinUntil(() => TryGetProcesses(out processes));
                    if(TryFindMainWindowForChangeTitle(out var mainWindow))
                        SetWindowText(mainWindow.MainWindowHandle, $"{mainWindow.MainWindowTitle} [Patched]");
                    if (TryAddActiveProcesses())
                        EnableExitedEventIfNotEnabled();
                    if (TryFindExitedProcesses(out var exitedPIDs))
                    {
                        exitedPIDs.AsParallel().ForAll(x => 
                        {
                            if(anchorProcesses.Remove(x))
                                logger.LogInformation($"Removed pid '{x}' as it is closed");
                        });
                    }
                    if (TryFindZombieProcesses(out var zombieProcesses))
                    {
                        zombieProcesses.AsParallel().ForAll(x => 
                        { 
                            x.Kill(true);
                            logger.LogInformation($"Killed PID '{x.Id}' as he is a zombie");
                        });
                    } 
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                finally 
                {
                    Task.Delay(1000).Wait();
                }
            }
        }

        bool TryGetProcesses(out Process[] findedProcesses)
        {
            findedProcesses = Process.GetProcessesByName(PROCESS_NAME);
            return findedProcesses != null;
        }

        bool TryFindExitedProcesses(out IEnumerable<int> exitedPIDs)
        {
            exitedPIDs = anchorProcesses.Keys.Where(id => anchorProcesses[id].HasExited);
            return exitedPIDs.Any();
        }

        bool TryFindZombieProcesses(out IEnumerable<Process> zombieProcesses)
        {
            zombieProcesses = processes.Where(x => !x.HasExited && !IsParentProcessExist(GetParentProcessId(x.Id)));
            return zombieProcesses.Any();
        }

        bool IsParentProcessExist(int processId)
        {
            if (processId == -1) return false;
            return Process.GetProcesses().Any(x => x.Id == processId);
        }

        int GetParentProcessId(int processId)
        {
            int parentId = -1;
            using ManagementObjectSearcher searcher = new($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            using ManagementObjectCollection objects = searcher.Get();
            foreach (ManagementObject obj in objects.Cast<ManagementObject>())
            {
                if (TryGetPropertyParentProcessId(obj, out parentId))
                    break;
            }
            if(parentId == -1)
                logger.LogInformation($"Failed to retrieve parent process for process id: {processId} => Zombie detected");
            return parentId;
        }

        bool TryGetPropertyParentProcessId(ManagementObject obj, out int parentId)
        {
            parentId = Convert.ToInt32(obj.GetPropertyValue("ParentProcessId"));
            return parentId != 0;
        }

        bool TryFindMainWindowForChangeTitle(out Process mainWindowProcess)
        {
            mainWindowProcess = processes.FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero && x.MainWindowTitle.StartsWith("Anchor") && !x.MainWindowTitle.Contains("[Patched]"));
            return mainWindowProcess != null;
        }

        bool TryAddActiveProcesses()
        {
            processes.Where(p => !anchorProcesses.ContainsKey(p.Id)).ToList().ForEach(p => anchorProcesses.TryAdd(p.Id, p));
            return anchorProcesses.Any();
        }

        void EnableExitedEventIfNotEnabled()
        {
            var p = anchorProcesses.Values.Where(x => x.MainWindowHandle != IntPtr.Zero).FirstOrDefault(x => !x.EnableRaisingEvents);
            if (p != null)
            {
                p.EnableRaisingEvents = true;
                p.Exited += MainAnchorProcess_Exited;
            }
        }

        void MainAnchorProcess_Exited(object sender, EventArgs e)
        {
            try
            {
                logger.LogInformation(TryForceCloseAllAnchorProcesses() ? "All Anchor processes are forcibly closed." : "All Anchor processes are normally closed.");
            }
            catch (Exception ex) { logger.LogError(ex.Message); }
            finally { anchorProcesses.Clear(); }
        }

        bool TryForceCloseAllAnchorProcesses()
        {
            var anchorProcesses = Process.GetProcessesByName(PROCESS_NAME);
            if (anchorProcesses.Any() && anchorProcesses.All(x => !x.HasExited))
            {
                anchorProcesses.Where(x => !x.HasExited).AsParallel().ForAll(ForceClose);
                return true;
            }
            return false;
        }

        void ForceClose(Process anchorProcess)
        {
            if(!anchorProcess.HasExited) anchorProcess.Kill(true);
        }

        void OnApplicationStopping()
        {
            isApplicationRunning = false;
            logger.LogInformation("Stop Watching");
        }
    }
}
