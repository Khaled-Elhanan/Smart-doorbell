using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Api.Demo;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell;
using Doorbell.Application.Features.Devices.Commands.CreateDevice;
using Doorbell.Application.Features.Users.Commands.CreateUser;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Doorbell.Tests.Demo;

public class DemoSimulationServiceTests
{
    [Fact]
    public async Task StartRingAsync_ConcurrentCalls_InitializesFixtureOnce()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();

        serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender)))
            .Returns(senderMock.Object);
        scopeMock.Setup(s => s.ServiceProvider)
            .Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(sf => sf.CreateScope())
            .Returns(scopeMock.Object);

        senderMock.Setup(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        senderMock.Setup(s => s.Send(It.IsAny<CreateDeviceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var publisherMock = new Mock<IMqttCommandPublisher>();
        var optionsMock = new Mock<IOptions<DemoOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new DemoOptions { SnapshotTimeoutSeconds = 30 });
        var envMock = new Mock<IWebHostEnvironment>();
        var loggerMock = new Mock<ILogger<DemoSimulationService>>();

        var service = new DemoSimulationService(
            scopeFactoryMock.Object,
            publisherMock.Object,
            optionsMock.Object,
            envMock.Object,
            loggerMock.Object);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => service.StartRingAsync(CancellationToken.None)))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        senderMock.Verify(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<CreateDeviceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<ConfigureDoorbellCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
