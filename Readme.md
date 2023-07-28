# SMS Microservice

SMS Microservice is a backend service responsible for reliably sending SMS to customers.

## Problem Statement

The primary function of this microservice is to ensure that SMS messages are sent to customers reliably. SMS messages often contain critical information, and as such, the service should do everything possible to make sure that these messages reach the customers. However, if exactly once semantics aren't possible, then the service may occasionally fail by sending an SMS message twice.

## Design and Patterns Used

The service is based on a command processing pattern. We have a `SendSmsCommand` that triggers the service to send an SMS, and an `SmsSent` event that the service publishes when an SMS has been sent.

For handling failures when sending SMS, we're using a Retry Policy from the Polly library. This policy is set to retry a specified number of times with exponential backoff and jitter strategy to avoid sudden bursts of retries.

We're also using Dependency Injection (DI) to manage dependencies and interfaces to abstract out the concrete implementations. This makes the code more maintainable and easier to test.

## Assumptions and Trade-offs

We're assuming that the third-party SMS sending API we're using is mostly reliable but might occasionally fail. We're also assuming that receiving an SMS message twice is annoying for a customer, but it is tolerable if it's a rare occurrence.

One trade-off we made was that, in case of a failure, we decided to retry sending the SMS a certain number of times before giving up, which could potentially lead to the same SMS being sent multiple times.

## Future Improvements

If more time was available, the following improvements could be made:
- Implement more detailed logging and error handling.

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

- .NET Core 7.0 or higher
- Polly library for .NET

### Installation

1. Clone the repository
2. Navigate to the project directory
3. Restore the packages
`   dotnet restore`

## Usage

This service receives `SendSmsCommand` that contains details about the SMS to be sent, including the recipient's phone number and the text of the SMS. After receiving the command, the service sends the SMS using a third-party API and publishes a `SmsSent` event.

The service includes a retry policy for handling failures when sending SMS, with exponential backoff and jitter strategy to avoid sudden bursts of retries.

## Running the Tests

You can run the tests using the following command:
`dotnet test`

The tests ensure that the service works as expected, including the retry logic in case of failures when sending the SMS.

