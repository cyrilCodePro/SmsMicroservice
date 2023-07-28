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
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;
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
       
        // Define a retry policy
        var retryPolicy = Policy<HttpResponseMessage>
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
   

        // Circuit Breaker policy:
        // Handles exceptions from HTTP responses.
        // If it encounters 3 consecutive exceptions, it will "open the circuit" -- stop trying the request -- for 1 minute.
        // During this time, any attempt to execute the action will fail immediately.
        // It will log when the circuit is opened and when it is reset.

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (ex, breakDelay) =>
                {
                    // This block is called when circuit breaker opens the circuit
                    logger.LogError($"Circuit breaker opened for {breakDelay.TotalSeconds} seconds due to: {ex.Message}");
                },
                onReset: () =>
                {
                    // This block is called when circuit breaker resets (closes the circuit)
                    logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    // This block is called when circuit breaker is half-open (after the durationOfBreak, before first trial)
                    logger.LogInformation("Circuit breaker half-open");
                }
            );
        // Wrap the retry policy with the circuit breaker
        

        _policy =retryPolicy.WrapAsync(circuitBreakerPolicy);
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
            var response = await _policy.ExecuteAsync(
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