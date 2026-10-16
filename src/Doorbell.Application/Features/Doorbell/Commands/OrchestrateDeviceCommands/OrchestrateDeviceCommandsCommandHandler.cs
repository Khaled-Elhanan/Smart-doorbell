using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Storage;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Doorbell.Commands.OrchestrateDeviceCommands;

public sealed class OrchestrateDeviceCommandsCommandHandler
    : IRequestHandler<OrchestrateDeviceCommandsCommand>
{
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IDoorbellCompositionRepository _compositionRepository;
    private readonly IDeviceCommandRepository _deviceCommandRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IMqttCommandPublisher _mqttCommandPublisher;
    private readonly IStorageKeyGenerator _storageKeyGenerator;
    private readonly IObjectStorage _objectStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrchestrateDeviceCommandsCommandHandler> _logger;

    private static readonly TimeSpan UploadUrlLifetime =
        TimeSpan.FromMinutes(5);

    public OrchestrateDeviceCommandsCommandHandler(
        IDoorbellRingEventRepository ringEventRepository,
        IDoorbellCompositionRepository compositionRepository,
        IDeviceCommandRepository deviceCommandRepository,
        IDeviceRepository deviceRepository,
        IOutboxRepository outboxRepository,
        IMqttCommandPublisher mqttCommandPublisher,
        IStorageKeyGenerator storageKeyGenerator,
        IObjectStorage objectStorage,
        IUnitOfWork unitOfWork,
        ILogger<OrchestrateDeviceCommandsCommandHandler> logger)
    {
        _ringEventRepository = ringEventRepository;
        _compositionRepository = compositionRepository;
        _deviceCommandRepository = deviceCommandRepository;
        _deviceRepository = deviceRepository;
        _outboxRepository = outboxRepository;
        _mqttCommandPublisher = mqttCommandPublisher;
        _storageKeyGenerator = storageKeyGenerator;
        _objectStorage = objectStorage;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        OrchestrateDeviceCommandsCommand request,
        CancellationToken cancellationToken)
    {
        var ringEvent =
            await _ringEventRepository.GetByIdAsync(
                request.RingEventId,
                cancellationToken);

        if (ringEvent is null)
        {
            throw new InvalidOperationException(
                $"Ring event '{request.RingEventId}' was not found.");
        }

        var composition =
            await _compositionRepository.GetByDeviceIdAsync(
                ringEvent.DeviceId,
                cancellationToken);

        if (composition is null)
        {
            _logger.LogWarning(
                "Ring event {RingEventId} has no doorbell composition for device {DeviceId}; orchestration skipped.",
                ringEvent.Id,
                ringEvent.DeviceId);

            return;
        }

        var snapshotRequestId =
            $"{ringEvent.DeviceEventId}:snapshot";

        var chimeRequestId =
            $"{ringEvent.DeviceEventId}:chime";

        var snapshotCommand =
            await _deviceCommandRepository.GetByRingEventIdAsync(
                ringEvent.Id,
                DeviceCommandType.Snapshot,
                cancellationToken);

        var chimeCommand =
            await _deviceCommandRepository.GetByRingEventIdAsync(
                ringEvent.Id,
                DeviceCommandType.Chime,
                cancellationToken);

        var intentChanged = false;

        if (snapshotCommand is null)
        {
            snapshotCommand = DeviceCommand.Create(
                ringEvent.Id,
                ringEvent.DeviceId,
                composition.CameraEntityCode,
                snapshotRequestId,
                DeviceCommandType.Snapshot,
                _storageKeyGenerator.CreateSnapshotKey(
                    ringEvent.DeviceId,
                    ringEvent.Id));

            await _deviceCommandRepository.AddAsync(
                snapshotCommand,
                cancellationToken);

            intentChanged = true;
        }

        if (chimeCommand is null)
        {
            chimeCommand = DeviceCommand.Create(
                ringEvent.Id,
                ringEvent.DeviceId,
                composition.ChimeEntityCode,
                chimeRequestId,
                DeviceCommandType.Chime);

            await _deviceCommandRepository.AddAsync(
                chimeCommand,
                cancellationToken);

            intentChanged = true;
        }

        if (ringEvent.SnapshotStatus == SnapshotStatus.NotRequested)
        {
            ringEvent.MarkSnapshotRequested();
            intentChanged = true;
        }

        if (ringEvent.ChimeStatus == ChimeStatus.NotRequested)
        {
            ringEvent.MarkChimeRequested();
            intentChanged = true;
        }

        if (intentChanged)
        {
            var ringAlert =
                await _outboxRepository.GetByRingEventIdAndKindAsync(
                    ringEvent.Id,
                    NotificationKind.RingAlert,
                    cancellationToken);

            if (ringAlert is null)
            {
                var device =
                    await _deviceRepository.GetByIdAsync(
                        ringEvent.DeviceId,
                        cancellationToken);

                if (device is null)
                {
                    throw new InvalidOperationException(
                        $"Device '{ringEvent.DeviceId}' for ring event '{ringEvent.Id}' was not found.");
                }

                await _outboxRepository.AddAsync(
                    NotificationOutbox.Create(
                        ringEvent.Id,
                        NotificationKind.RingAlert,
                        device.OwnerUserId),
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Recorded orchestration intent for ring event {RingEventId}. SnapshotRequestId: {SnapshotRequestId}, ChimeRequestId: {ChimeRequestId}",
                ringEvent.Id,
                snapshotRequestId,
                chimeRequestId);
        }

        var published = false;

        if (snapshotCommand.Status == DeviceCommandStatus.Pending)
        {
            var uploadUrl = await _objectStorage.CreateUploadUrlAsync(
                snapshotCommand.StorageKey!,
                UploadUrlLifetime,
                cancellationToken);

            await _mqttCommandPublisher.PublishAsync(
                composition.SnapshotCommandTopic,
                new SnapshotCommandPayload(
                    snapshotRequestId,
                    ringEvent.DeviceId,
                    uploadUrl.ToString()),
                cancellationToken);

            snapshotCommand.MarkSent();
            published = true;

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Published snapshot command for ring event {RingEventId}. RequestId: {RequestId}, StorageKey: {StorageKey}",
                ringEvent.Id,
                snapshotRequestId,
                snapshotCommand.StorageKey);
        }

        if (chimeCommand.Status == DeviceCommandStatus.Pending)
        {
            await _mqttCommandPublisher.PublishAsync(
                composition.ChimeCommandTopic,
                new ChimeCommandPayload(
                    chimeRequestId,
                    composition.DefaultChimeMelodyId),
                cancellationToken);

            chimeCommand.MarkSent();
            published = true;

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Published chime command for ring event {RingEventId}. RequestId: {RequestId}, MelodyId: {MelodyId}",
                ringEvent.Id,
                chimeRequestId,
                composition.DefaultChimeMelodyId);
        }

        if (!intentChanged && !published)
        {
            _logger.LogInformation(
                "Orchestration already complete for ring event {RingEventId}; no-op.",
                ringEvent.Id);
        }
    }
}
