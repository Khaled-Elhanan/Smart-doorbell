using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Api.Controllers;
using Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;
using Doorbell.Application.Features.Doorbell.Commands.OrchestrateDeviceCommands;
using Doorbell.Application.Features.Doorbell.Commands.ReceiveRing;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Tests.Controllers;

public class DoorbellControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly DoorbellController _controller;

    public DoorbellControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new DoorbellController(_senderMock.Object);
    }

    [Fact]
    public async Task ReceiveRing_WhenNewRing_OrchestratesCommands()
    {
        // Arrange
        var command = new ReceiveRingCommand(Guid.NewGuid(), "event-123", DateTime.UtcNow);
        var expectedRingEventId = Guid.NewGuid();
        
        _senderMock.Setup(s => s.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveRingResult(expectedRingEventId, true));

        // Act
        var result = await _controller.ReceiveRing(command, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        var ringEventIdProperty = value.GetType().GetProperty("ringEventId");
        Assert.NotNull(ringEventIdProperty);
        Assert.Equal(expectedRingEventId, ringEventIdProperty.GetValue(value));

        _senderMock.Verify(s => s.Send(It.Is<OrchestrateDeviceCommandsCommand>(c => c.RingEventId == expectedRingEventId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveRing_WhenDuplicateRing_DoesNotOrchestrateCommands()
    {
        // Arrange
        var command = new ReceiveRingCommand(Guid.NewGuid(), "event-123", DateTime.UtcNow);
        var expectedRingEventId = Guid.NewGuid();
        
        _senderMock.Setup(s => s.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveRingResult(expectedRingEventId, false));

        // Act
        var result = await _controller.ReceiveRing(command, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        var ringEventIdProperty = value.GetType().GetProperty("ringEventId");
        Assert.NotNull(ringEventIdProperty);
        Assert.Equal(expectedRingEventId, ringEventIdProperty.GetValue(value));

        _senderMock.Verify(s => s.Send(It.IsAny<OrchestrateDeviceCommandsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveRing_WhenDbUpdateExceptionDuringOrchestration_ReturnsOk()
    {
        // Arrange
        var command = new ReceiveRingCommand(Guid.NewGuid(), "event-123", DateTime.UtcNow);
        var expectedRingEventId = Guid.NewGuid();
        
        _senderMock.Setup(s => s.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveRingResult(expectedRingEventId, true));

        _senderMock.Setup(s => s.Send(It.IsAny<OrchestrateDeviceCommandsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Concurrency conflict"));

        // Act
        var result = await _controller.ReceiveRing(command, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        var ringEventIdProperty = value.GetType().GetProperty("ringEventId");
        Assert.NotNull(ringEventIdProperty);
        Assert.Equal(expectedRingEventId, ringEventIdProperty.GetValue(value));
    }

    [Fact]
    public async Task AcknowledgeRing_ReturnsAcknowledgedPayload()
    {
        var ringEventId = Guid.NewGuid();
        var acknowledgedAt = DateTime.UtcNow;

        _senderMock.Setup(s => s.Send(
                It.IsAny<AcknowledgeRingCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcknowledgeRingResult(ringEventId, true, false, acknowledgedAt));

        var result = await _controller.AcknowledgeRing(ringEventId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        Assert.Equal(ringEventId, value.GetType().GetProperty("ringEventId")!.GetValue(value));
        Assert.Equal(true, value.GetType().GetProperty("acknowledged")!.GetValue(value));
        Assert.Equal(false, value.GetType().GetProperty("alreadyAcknowledged")!.GetValue(value));

        _senderMock.Verify(s => s.Send(
            It.Is<AcknowledgeRingCommand>(c => c.RingEventId == ringEventId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcknowledgeRing_WhenAlreadyAcknowledged_ReportsIdempotentResult()
    {
        var ringEventId = Guid.NewGuid();
        var acknowledgedAt = DateTime.UtcNow.AddMinutes(-5);

        _senderMock.Setup(s => s.Send(
                It.IsAny<AcknowledgeRingCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcknowledgeRingResult(ringEventId, true, true, acknowledgedAt));

        var result = await _controller.AcknowledgeRing(ringEventId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        Assert.Equal(true, value.GetType().GetProperty("acknowledged")!.GetValue(value));
        Assert.Equal(true, value.GetType().GetProperty("alreadyAcknowledged")!.GetValue(value));
    }
}
