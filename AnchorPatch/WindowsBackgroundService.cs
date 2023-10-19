using AnchorPatch.Services;

namespace AnchorPatch
{
    public class WindowsBackgroundService : BackgroundService
    {
        readonly PatchService patchService;
        public WindowsBackgroundService(PatchService patchService) => this.patchService = patchService;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            patchService.Start();

            await Task.CompletedTask;
        }
    }
}