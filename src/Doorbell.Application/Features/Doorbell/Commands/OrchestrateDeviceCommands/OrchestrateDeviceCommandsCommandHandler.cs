using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
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
    private readonly IMqttCommandPublisher _mqttCommandPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrchestrateDeviceCommandsCommandHandler> _logger;

    public OrchestrateDeviceCommandsCommandHandler(
        IDoorbellRingEventRepository ringEventRepository,
        IDoorbellCompositionRepository compositionRepository,
        IDeviceCommandRepository deviceCommandRepository,
        IMqttCommandPublisher mqttCommandPublisher,
        IUnitOfWork unitOfWork,
        ILogger<OrchestrateDeviceCommandsCommandHandler> logger)
    {
        _ringEventRepository = ringEventRepository;
        _compositionRepository = compositionRepository;
        _deviceCommandRepository = deviceCommandRepository;
        _mqttCommandPublisher = mqttCommandPublisher;
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
            throw new InvalidOperationException(
                $"No doorbell composition found for device '{ringEvent.DeviceId}'.");
        }

        var snapshotRequestId =
            $"{ringEvent.DeviceEventId}:snapshot";

        var chimeRequestId =
            $"{ringEvent.DeviceEventId}:chime";

        var snapshotCommand =
            DeviceCommand.Create(
                ringEvent.Id,
                ringEvent.DeviceId,
                composition.CameraEntityCode,
                snapshotRequestId,
                DeviceCommandType.Snapshot);

        var chimeCommand =
            DeviceCommand.Create(
                ringEvent.Id,
                ringEvent.DeviceId,
                composition.ChimeEntityCode,
                chimeRequestId,
                DeviceCommandType.Chime);

        await _deviceCommandRepository.AddAsync(
            snapshotCommand,
            cancellationToken); 

        await _deviceCommandRepository.AddAsync(
            chimeCommand,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Created device commands for RingEvent {RingEventId}. SnapshotRequestId: {SnapshotRequestId}, ChimeRequestId: {ChimeRequestId}",
            ringEvent.Id,
            snapshotRequestId,
            chimeRequestId);

        var snapshotPayload =
            new SnapshotCommandPayload(
                snapshotRequestId,
                ringEvent.DeviceId);

        await _mqttCommandPublisher.PublishAsync(
            composition.SnapshotCommandTopic,
            snapshotPayload,
            cancellationToken);

        snapshotCommand.MarkSent();
        ringEvent.MarkSnapshotRequested();

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Snapshot command sent for RingEvent {RingEventId}. RequestId: {RequestId}",
            ringEvent.Id,
            snapshotRequestId);

        var chimePayload =
            new ChimeCommandPayload(
                chimeRequestId,
                composition.DefaultChimeMelodyId);

        await _mqttCommandPublisher.PublishAsync(
            composition.ChimeCommandTopic,
            chimePayload,
            cancellationToken);

        chimeCommand.MarkSent();
        ringEvent.MarkChimeRequested();

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Chime command sent for RingEvent {RingEventId}. RequestId: {RequestId}, MelodyId: {MelodyId}",
            ringEvent.Id,
            chimeRequestId,
            composition.DefaultChimeMelodyId);
    }
}