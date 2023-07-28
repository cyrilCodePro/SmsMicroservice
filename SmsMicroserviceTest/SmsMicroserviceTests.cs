using Moq;
using System.Net;
using SmsMicroservice.Contracts;
using SmsMicroservice.Events;
using SmsMicroservice.Logger;
using SmsMicroservice.Queue;
using SmsMicroservice.RestClients;

namespace SmsMicroserviceTest;
  
public class SmsMicroserviceTests

{
      private readonly Mock<IMessageQueue<SendSmsCommand>> _messageQueueMock = new Mock<IMessageQueue<SendSmsCommand>>();
      private readonly Mock<ISmsApiClient<SendSmsCommand, HttpResponseMessage>> _httpClientMock = new Mock<ISmsApiClient<SendSmsCommand, HttpResponseMessage>>();
      private readonly Mock<IEventBus<SmsSent>> _eventBusMock = new Mock<IEventBus<SmsSent>>();
      private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
  
      [Fact]
      public async Task ProcessCommandAsync_SendSmsCommand_Successfully()
      {
          // Arrange
          // Creating a new SMS command to send
          var command = new SendSmsCommand { IdempotencyKey = Guid.NewGuid(), PhoneNumber = "123456789", SmsText = "Hello" };
  
          // Mocking a successful HTTP POST response
          _httpClientMock.Setup(x => x.PostAsync(command))
              .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
  
          // Act
          // Creating the service and processing the command
          var service = new SmsMicroservice.SmsMicroservice(_messageQueueMock.Object, _httpClientMock.Object, _eventBusMock.Object, _loggerMock.Object);
          await service.ProcessCommandAsync(command, CancellationToken.None);
  
          // Assert
          // Verifying that an SMS sent event was published and no errors were logged
          _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<SmsSent>()), Times.Once);
          _loggerMock.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
      }
  
      [Fact]
      public async Task ProcessCommandAsync_DuplicateSendSmsCommand_IgnoresDuplicate()
      {
          // Arrange
          // Creating a new SMS command to send
          var command = new SendSmsCommand { IdempotencyKey = Guid.NewGuid(), PhoneNumber = "123456789", SmsText = "Hello" };
  
          // Mocking a successful HTTP POST response
          _httpClientMock.Setup(x => x.PostAsync(command))
              .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
  
          // Act
          // Creating the service and processing the command twice (duplicating the command)
          var service = new SmsMicroservice.SmsMicroservice(_messageQueueMock.Object, _httpClientMock.Object, _eventBusMock.Object, _loggerMock.Object);
          await service.ProcessCommandAsync(command, CancellationToken.None);
          await service.ProcessCommandAsync(command, CancellationToken.None); // duplicate command
  
          // Assert
          // Verifying that an SMS sent event was published only once and no errors were logged
          _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<SmsSent>()), Times.Once); // should still only be called once
          _loggerMock.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
      }
  
      [Fact]
      public async Task ProcessCommandAsync_FailedSendSmsCommand_RetriesAndLogsError()
      {
          // Arrange
          // Creating a new SMS command to send
          var command = new SendSmsCommand { IdempotencyKey = Guid.NewGuid(), PhoneNumber = "123456789", SmsText = "Hello" };

          // Mocking a failed HTTP POST response
          _httpClientMock.Setup(x => x.PostAsync(command))
              .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

          // Act
          // Creating the service and processing the command
          var service = new SmsMicroservice.SmsMicroservice(_messageQueueMock.Object, _httpClientMock.Object, _eventBusMock.Object, _loggerMock.Object);
          await service.ProcessCommandAsync(command, CancellationToken.None);

          // Assert
          // Verifying that no SMS sent event was published, an error was logged and the command was re-enqueued
          _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<SmsSent>()), Times.Never);
          _loggerMock.Verify(x => x.LogError(It.IsAny<string>()), Times.Exactly(4));
          _messageQueueMock.Verify(x => x.EnqueueAsync(command), Times.Once);
      }
  
      [Fact]
      public async Task ProcessCommandAsync_MultipleDistinctSendSmsCommands_Successfully()
      {
          // Arrange
          // Creating two distinct SMS commands to send
          var command1 = new SendSmsCommand { IdempotencyKey = Guid.NewGuid(), PhoneNumber = "123456789", SmsText = "Hello1" };
          var command2 = new SendSmsCommand { IdempotencyKey = Guid.NewGuid(), PhoneNumber = "987654321", SmsText = "Hello2" };
  
          // Mocking a successful HTTP POST response for both commands
          _httpClientMock.Setup(x => x.PostAsync(It.IsAny<SendSmsCommand>()))
              .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
  
          // Act
          // Creating the service and processing both commands
          var service = new SmsMicroservice.SmsMicroservice(_messageQueueMock.Object, _httpClientMock.Object, _eventBusMock.Object, _loggerMock.Object);
          await service.ProcessCommandAsync(command1, CancellationToken.None);
          await service.ProcessCommandAsync(command2, CancellationToken.None);
  
          // Assert
          // Verifying that an SMS sent event was published twice and no errors were logged
          _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<SmsSent>()), Times.Exactly(2));
          _loggerMock.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
      }
  }
