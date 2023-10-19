using AnchorPatch.Services;

namespace AnchorPatch
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(services =>
                {
                    services.AddWindowsService();
                    services.AddSingleton<PatchService>();
                    services.AddHostedService<WindowsBackgroundService>();
                })
                .Build();

            host.Run();
        }
    }
}