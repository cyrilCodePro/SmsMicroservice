namespace SmsMicroservice.Events;

public class SmsSent
{
    public string PhoneNumber { get; set; }
    public string SmsText { get; set; }
}