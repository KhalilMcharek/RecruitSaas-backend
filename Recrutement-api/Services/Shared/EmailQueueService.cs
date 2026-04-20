﻿using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Recrutement_api.Configuration;
using Recrutement_api.DTOs.Email;
using Recrutement_api.Services.Shared;

namespace Recrutement_api.Services.Shared;


/// Service de queue d'emails avec retry automatique.
/// Enqueue un email → le BackgroundWorker l'envoie de manière asynchrone.

public class EmailQueueService : IHostedService, IDisposable
{
    private readonly ConcurrentQueue<EmailQueueItem> _queue = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailQueueService> _logger;
    private Timer? _timer;
    private bool _isProcessing = false;

    public EmailQueueService(IServiceProvider serviceProvider, ILogger<EmailQueueService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    //  IHostedService 
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EmailQueueService started.");
       
        _timer = new Timer(ProcessQueue, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EmailQueueService stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

   
    public void EnqueueExpertInvitation(ExpertInvitationEmailDto dto) =>
        Enqueue("ExpertInvitation", dto);

    public void EnqueueWelcome(WelcomeEmailDto dto) =>
        Enqueue("Welcome", dto);

    public void EnqueuePasswordReset(PasswordResetEmailDto dto) =>
        Enqueue("PasswordReset", dto);

    public void EnqueueGeneric(GenericEmailDto dto) =>
        Enqueue("Generic", dto);

    private void Enqueue<T>(string type, T payload)
    {
        var item = new EmailQueueItem
        {
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload)
        };
        _queue.Enqueue(item);
        _logger.LogInformation("Email queued: Type={Type}, Id={Id}", type, item.Id);
    }

   
    private async void ProcessQueue(object? state)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var pending = new List<EmailQueueItem>();

          
            while (_queue.TryDequeue(out var item))
                pending.Add(item);

            if (pending.Count == 0) return;

            _logger.LogInformation("Processing {Count} queued email(s)...", pending.Count);

            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

            foreach (var item in pending)
            {
                await ProcessItem(item, emailService);
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessItem(EmailQueueItem item, EmailService emailService)
    {
        item.Status = EmailQueueStatus.Processing;
        item.LastAttemptAt = DateTime.UtcNow;

        try
        {
            switch (item.Type)
            {
                case "ExpertInvitation":
                    var invitDto = JsonSerializer.Deserialize<ExpertInvitationEmailDto>(item.PayloadJson)!;
                    await emailService.SendExpertInvitationAsync(invitDto);
                    break;

                case "Welcome":
                    var welcomeDto = JsonSerializer.Deserialize<WelcomeEmailDto>(item.PayloadJson)!;
                    await emailService.SendWelcomeEmailAsync(welcomeDto);
                    break;

                case "PasswordReset":
                    var resetDto = JsonSerializer.Deserialize<PasswordResetEmailDto>(item.PayloadJson)!;
                    await emailService.SendPasswordResetAsync(resetDto);
                    break;

                case "Generic":
                    var genericDto = JsonSerializer.Deserialize<GenericEmailDto>(item.PayloadJson)!;
                    
                    await emailService.SendGenericAsync(genericDto);
                    break;

                default:
                    _logger.LogWarning("Unknown email type: {Type}", item.Type);
                    item.Status = EmailQueueStatus.Failed;
                    return;
            }

            item.Status = EmailQueueStatus.Sent;
            _logger.LogInformation("Email sent successfully: Id={Id}, Type={Type}", item.Id, item.Type);
        }
        catch (Exception ex)
        {
            item.RetryCount++;
            item.LastError = ex.Message;
            _logger.LogError(ex, "Failed to send email Id={Id}, Type={Type}, Attempt={Attempt}", item.Id, item.Type, item.RetryCount);

            if (item.RetryCount < item.MaxRetries)
            {
                item.Status = EmailQueueStatus.Pending;
                _queue.Enqueue(item); 
                _logger.LogInformation("Email re-queued for retry: Id={Id}, Attempt {Attempt}/{Max}", item.Id, item.RetryCount, item.MaxRetries);
            }
            else
            {
                item.Status = EmailQueueStatus.Failed;
                _logger.LogError("Email permanently failed after {Max} attempts: Id={Id}, Type={Type}", item.MaxRetries, item.Id, item.Type);
            }
        }
    }

    public void Dispose() => _timer?.Dispose();
}