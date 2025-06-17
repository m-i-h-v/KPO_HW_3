using Microsoft.EntityFrameworkCore;

namespace PaymentsService.Database;

internal sealed class MigrationRunner : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MigrationRunner(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<PaymentContext>();

        for (int i = 0; i < 5; i++)
        {
            try
            {
                await context.Database.MigrateAsync(cancellationToken);
                break;
            }
            catch (Exception ex)
            {
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}