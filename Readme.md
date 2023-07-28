# SMS Microservice

SMS Microservice is a backend service responsible for sending SMS to customers.

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

