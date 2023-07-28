using Polly;
using System.Collections.Concurrent;
using SmsMicroservice.Contracts;
using SmsMicroservice.Events;
using SmsMicroservice.Logger;
using SmsMicroservice.Queue;
using SmsMicroservice.RestClients;

namespace SmsMicroservice;
public class SmsMicroservice
{
    private readonly IMessageQueue<SendSmsCommand> _messageQueue;
    private readonly ISmsApiClient<SendSmsCommand, HttpResponseMessage> _httpClient;
    private readonly IEventBus<SmsSent> _eventBus;
    private readonly ILogger _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ConcurrentDictionary<Guid, byte> _processedCommands;

    public SmsMicroservice(
        IMessageQueue<SendSmsCommand> messageQueue, 
        ISmsApiClient<SendSmsCommand, HttpResponseMessage> httpClient, 
        IEventBus<SmsSent> eventBus, 
        ILogger logger)
    {
        _messageQueue = messageQueue;
        _httpClient = httpClient;
        _eventBus = eventBus;
        _logger = logger;
        _processedCommands = new ConcurrentDictionary<Guid, byte>();
        /*
         * The WaitAndRetryAsync policy, specifically, is designed to wait and retry a certain number of times whenever a handled exception or return result is detected.
         * The specifics of the policy are as follows:
         * 3 retries: The policy will attempt to retry the action up to 3 times if it fails.
         * exponentialBackoff duration: The duration to wait between retries starts at 2 seconds and doubles each time, with a maximum wait time of 30 seconds.
         * onRetryAsync: This is a delegate that is called before each retry. In this case, it logs a message and re-queues the SMS command for later processing.
         * For example, if a network failure occurs when trying to send an SMS, the policy will catch the exception, wait for a certain amount of time
         * (according to the exponential backoff strategy), then attempt to send the SMS again. If it still fails, the policy will wait and retry again,
         * up to a maximum of 3 retries.
         */
        _retryPolicy = Policy<HttpResponseMessage>
            .HandleResult(message => !message.IsSuccessStatusCode)
            .Or<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    if (exception.Exception != null)
                    {
                        _logger.LogError($"An error occurred while sending SMS: {exception.Exception.Message}. Retry attempt {retryCount}.");
                    }
                    else if (!exception.Result.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to send SMS with status code {exception.Result.StatusCode}. Retry attempt {retryCount}.");
                    }
                }
            );
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var command = await _messageQueue.DequeueAsync();
            await ProcessCommandAsync(command, cancellationToken);
        }
    }

    public async Task ProcessCommandAsync(SendSmsCommand command, CancellationToken cancellationToken)
    {
        // Ignore if this command has already been processed
        if (_processedCommands.TryAdd(command.IdempotencyKey, 0))
        {
            var response = await _retryPolicy.ExecuteAsync(
                () => _httpClient.PostAsync(command)
            );

            if (response.IsSuccessStatusCode)
            {
                var smsSent = new SmsSent { PhoneNumber = command.PhoneNumber, SmsText = command.SmsText };
                await _eventBus.PublishAsync(smsSent);
            }
            else
            {
                _logger.LogError($"Failed to send SMS to {command.PhoneNumber} after 3 attempts.");
                // Re-queue the command for another attempt later
                await _messageQueue.EnqueueAsync(command);
            }
        }
    }
}