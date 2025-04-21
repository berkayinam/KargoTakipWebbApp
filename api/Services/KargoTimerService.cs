using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using KargoTakip.Services;

public class KargoTimerService : BackgroundService
{
    private readonly KargoService _kargoService;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

    public KargoTimerService(KargoService kargoService)
    {
        _kargoService = kargoService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _kargoService.CheckKargoStatuses();
            }
            catch (Exception)
            {
                // Log error if needed
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
} 