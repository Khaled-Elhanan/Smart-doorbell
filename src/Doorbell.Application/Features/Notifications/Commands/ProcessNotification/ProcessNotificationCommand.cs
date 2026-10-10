using System;
using MediatR;

namespace Doorbell.Application.Features.Notifications.Commands.ProcessNotification;

public sealed record ProcessNotificationCommand(Guid OutboxId) : IRequest<bool>;
