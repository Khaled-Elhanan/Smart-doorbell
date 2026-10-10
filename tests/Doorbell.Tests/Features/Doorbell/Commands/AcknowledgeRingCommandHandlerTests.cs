using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using Moq;
using Xunit;

namespace Doorbell.Tests.Features.Doorbell.Commands;

public class AcknowledgeRingCommandHandlerTests
{
    private readonly Mock<IDoorbellRingEventRepository> _ringRepoMock = new();
    private readonly Mock<IDeviceRepository> _deviceRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();

    private AcknowledgeRingCommandHandler CreateHandler()
    {
        return new AcknowledgeRingCommandHandler(
            _ringRepoMock.Object,
            _deviceRepoMock.Object,
            _uowMock.Object,
            _currentUserMock.Object);
    }

    private void SetupAuthenticatedUser(Guid? userId)
    {
        _currentUserMock.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUserMock
            .Setup(u => u.GetUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
    }

    private void SetupRingAndDevice(DoorbellRingEvent ring, Guid ownerUserId)
    {
        _ringRepoMock
            .Setup(r => r.GetByIdAsync(ring.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);

        _deviceRepoMock
            .Setup(d => d.GetByIdAsync(ring.DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Device(ownerUserId, "doorbell"));
    }

    [Fact]
    public async Task Handle_OwnerAcknowledges_RingBecomesAcknowledged()
    {
        var ownerId = Guid.NewGuid();
        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");

        SetupAuthenticatedUser(ownerId);
        SetupRingAndDevice(ring, ownerId);

        var result = await CreateHandler().Handle(
            new AcknowledgeRingCommand(ring.Id),
            CancellationToken.None);

        Assert.True(result.WasAcknowledged);
        Assert.False(result.AlreadyAcknowledged);
        Assert.Equal(AcknowledgeStatus.Acknowledged, ring.AcknowledgeStatus);
        Assert.NotNull(ring.AcknowledgedAtUtc);
        Assert.NotNull(result.AcknowledgedAtUtc);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateAcknowledge_IsIdempotentAndDoesNotSave()
    {
        var ownerId = Guid.NewGuid();
        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");
        ring.Acknowledge();
        var firstAcknowledgedAt = ring.AcknowledgedAtUtc;

        SetupAuthenticatedUser(ownerId);
        SetupRingAndDevice(ring, ownerId);

        var result = await CreateHandler().Handle(
            new AcknowledgeRingCommand(ring.Id),
            CancellationToken.None);

        Assert.True(result.WasAcknowledged);
        Assert.True(result.AlreadyAcknowledged);
        Assert.Equal(firstAcknowledgedAt, ring.AcknowledgedAtUtc);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnacknowledgedMissedEvent_CanStillBeAcknowledged()
    {
        var ownerId = Guid.NewGuid();
        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");
        ring.MarkMissed();

        SetupAuthenticatedUser(ownerId);
        SetupRingAndDevice(ring, ownerId);

        var result = await CreateHandler().Handle(
            new AcknowledgeRingCommand(ring.Id),
            CancellationToken.None);

        Assert.False(result.AlreadyAcknowledged);
        Assert.Equal(AcknowledgeStatus.Acknowledged, ring.AcknowledgeStatus);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorized()
    {
        _currentUserMock.SetupGet(u => u.IsAuthenticated).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateHandler().Handle(
                new AcknowledgeRingCommand(Guid.NewGuid()),
                CancellationToken.None));

        _ringRepoMock.Verify(r => r.GetByIdAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AuthenticatedButUnknownUser_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateHandler().Handle(
                new AcknowledgeRingCommand(Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RingNotFound_ThrowsNotFound()
    {
        SetupAuthenticatedUser(Guid.NewGuid());

        _ringRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoorbellRingEvent?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().Handle(
                new AcknowledgeRingCommand(Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeviceNotFound_ThrowsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");

        SetupAuthenticatedUser(ownerId);

        _ringRepoMock
            .Setup(r => r.GetByIdAsync(ring.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);
        _deviceRepoMock
            .Setup(d => d.GetByIdAsync(ring.DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().Handle(
                new AcknowledgeRingCommand(ring.Id),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbiddenAndDoesNotPersist()
    {
        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");

        SetupAuthenticatedUser(Guid.NewGuid());
        SetupRingAndDevice(ring, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateHandler().Handle(
                new AcknowledgeRingCommand(ring.Id),
                CancellationToken.None));

        Assert.Equal(AcknowledgeStatus.Pending, ring.AcknowledgeStatus);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
