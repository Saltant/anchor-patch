namespace AnchorPatch
{
    public class ServiceLogger
    {
        readonly DirectoryInfo logDir;
        public ServiceLogger() 
        {
            logDir = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
            if(!logDir.Exists)
            {
                throw new Exception("Log directory not exists");
            }
        }

        public void LogInformation(string message) => Log(LogLevel.Information, message);

        void Log(LogLevel logLevel, string message)
        {
            using var fs = File.AppendText(Path.Combine(logDir.FullName, GetCurrentLogFileName()));
            fs.WriteLine(string.Join(' ', $"[{logLevel}]({DateTime.Now:HH:mm:ss})", message));
        }

        static string GetCurrentLogFileName() => string.Join('_', "log", DateTime.Now.ToString("dd-MM-yyyy") + ".txt");
    }
}
