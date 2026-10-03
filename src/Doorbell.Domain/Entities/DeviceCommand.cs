using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;

namespace Doorbell.Domain.Entities;

public sealed class DeviceCommand
{
    public Guid Id { get; private set; }

    public Guid RingEventId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string EntityCode { get; private set; } = null!;

    public string RequestId { get; private set; } = null!;

    public DeviceCommandType CommandType { get; private set; }

    public DeviceCommandStatus Status { get; private set; }

    public int Attempt { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? ErrorMessage { get; private set; }

    private DeviceCommand()
    {
    }

    private DeviceCommand(
        Guid ringEventId,
        Guid deviceId,
        string entityCode,
        string requestId,
        DeviceCommandType commandType)
    {
        if (ringEventId == Guid.Empty)
            throw new DomainException("RingEventId cannot be empty.");

        if (deviceId == Guid.Empty)
            throw new DomainException("DeviceId cannot be empty.");

        if (string.IsNullOrWhiteSpace(entityCode))
            throw new DomainException("Entity code is required.");

        if (string.IsNullOrWhiteSpace(requestId))
            throw new DomainException("RequestId is required.");

        Id = Guid.NewGuid();

        RingEventId = ringEventId;
        DeviceId = deviceId;
        EntityCode = entityCode;
        RequestId = requestId;
        CommandType = commandType;

        Status = DeviceCommandStatus.Pending;
        Attempt = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static DeviceCommand Create(
        Guid ringEventId,
        Guid deviceId,
        string entityCode,
        string requestId,
        DeviceCommandType commandType)
    {
        return new DeviceCommand(
            ringEventId,
            deviceId,
            entityCode,
            requestId,
            commandType);
    }

    public void MarkSent()
    {
        if (Status != DeviceCommandStatus.Pending)
            throw new DomainException(
                "Only pending commands can be marked as sent.");

        Status = DeviceCommandStatus.Sent;
        Attempt++;
        SentAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        if (Status != DeviceCommandStatus.Sent)
            throw new DomainException(
                "Only sent commands can be completed.");

        Status = DeviceCommandStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        if (Status != DeviceCommandStatus.Sent)
            throw new DomainException(
                "Only sent commands can fail.");

        Status = DeviceCommandStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }
}