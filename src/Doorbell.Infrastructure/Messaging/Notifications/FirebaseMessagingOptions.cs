namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class FirebaseMessagingOptions
{
    public const string SectionName = "Firebase";

    public string? CredentialsPath { get; set; }
    public string? ProjectId { get; set; }
}
