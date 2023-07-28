namespace SmsMicroservice.Contracts;

public class SendSmsCommand
{
    public Guid IdempotencyKey { get; set; }
    public string PhoneNumber { get; set; }
    public string SmsText { get; set; }
}
