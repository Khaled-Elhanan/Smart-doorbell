using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class FirebaseFcmSender : IFcmNotificationSender
{
    private const string AppName = "doorbell-fcm";

    private static readonly object SharedAppLock = new();
    private static FirebaseApp? _sharedApp;

    private readonly FirebaseMessagingOptions _options;
    private readonly IUserDeviceTokenRepository _tokenRepository;
    private readonly ILogger<FirebaseFcmSender> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly object _stateLock = new();

    private bool _initialized;
    private FirebaseMessaging? _messaging;
    private string _configurationStatus = "FCM has not been initialized.";

    public FirebaseFcmSender(
        IOptions<FirebaseMessagingOptions> options,
        IUserDeviceTokenRepository tokenRepository,
        ILogger<FirebaseFcmSender> logger)
    {
        _options = options.Value;
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            EnsureInitialized();
            lock (_stateLock)
            {
                return _messaging is not null;
            }
        }
    }

    public string ConfigurationStatus
    {
        get
        {
            EnsureInitialized();
            lock (_stateLock)
            {
                return _configurationStatus;
            }
        }
    }

    public async Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();

        FirebaseMessaging? messaging;
        lock (_stateLock)
        {
            messaging = _messaging;
        }

        if (messaging is null)
        {
            return new NotificationSendResult(
                IsSuccess: false,
                IsTransient: false,
                ErrorMessage: $"FCM is not configured: {ConfigurationStatus}");
        }

        var tokens = await _tokenRepository.GetByUserIdAsync(userId, cancellationToken);

        if (tokens.Count == 0)
        {
            return new NotificationSendResult(
                IsSuccess: false,
                IsTransient: false,
                ErrorMessage: $"No FCM device token is registered for user '{userId}'.");
        }

        var data = BuildData(userId, message);

        var successCount = 0;
        var transient = false;
        var errors = new List<string>();

        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fcmMessage = new Message
                {
                    Token = token.Token,
                    Data = data,
                    Notification = new Notification
                    {
                        Title = "Doorbell",
                        Body = "Someone is at the door."
                    }
                };

                await messaging.SendAsync(fcmMessage, cancellationToken);
                successCount++;
            }
            catch (FirebaseMessagingException ex) when (IsInvalidToken(ex))
            {
                _logger.LogWarning(
                    "Removing invalid FCM token for user {UserId}. Code: {Code}",
                    userId,
                    ex.MessagingErrorCode);

                await _tokenRepository.RemoveAsync(userId, token.Token, cancellationToken);
                errors.Add($"invalid token removed ({ex.MessagingErrorCode})");
            }
            catch (FirebaseMessagingException ex)
            {
                if (IsTransient(ex))
                {
                    transient = true;
                }

                errors.Add($"{ex.MessagingErrorCode}: {ex.Message}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        if (successCount > 0)
        {
            return new NotificationSendResult(true, false, null);
        }

        return new NotificationSendResult(
            IsSuccess: false,
            IsTransient: transient,
            ErrorMessage: string.Join(" | ", errors));
    }

    private static Dictionary<string, string> BuildData(Guid userId, NotificationMessage message)
    {
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = message.NotificationId.ToString(),
            ["ringEventId"] = message.RingEventId.ToString(),
            ["deviceId"] = message.DeviceId.ToString(),
            ["ringTimestampUtc"] = message.RingTimestampUtc.ToString("O"),
            ["recipientUserId"] = userId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(message.SnapshotStorageKey))
        {
            data["snapshotStorageKey"] = message.SnapshotStorageKey!;
        }

        if (!string.IsNullOrWhiteSpace(message.SnapshotResolution))
        {
            data["snapshotResolution"] = message.SnapshotResolution!;
        }

        return data;
    }

    private static bool IsInvalidToken(FirebaseMessagingException exception)
    {
        return exception.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.InvalidArgument
            or MessagingErrorCode.SenderIdMismatch;
    }

    private static bool IsTransient(FirebaseMessagingException exception)
    {
        return exception.MessagingErrorCode is MessagingErrorCode.Unavailable
            or MessagingErrorCode.Internal
            or MessagingErrorCode.QuotaExceeded;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initLock.Wait();

        try
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                var credentialsPath = ResolveCredentialsPath();

                if (credentialsPath is null)
                {
                    SetStatus(
                        "Firebase credentials are not configured. Set "
                        + "'Firebase:CredentialsPath' or the "
                        + "GOOGLE_APPLICATION_CREDENTIALS environment variable.");
                    return;
                }

                if (!File.Exists(credentialsPath))
                {
                    SetStatus($"Firebase credentials file '{credentialsPath}' was not found.");
                    return;
                }

                lock (SharedAppLock)
                {
                    _sharedApp ??= FirebaseApp.Create(
                        new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(credentialsPath),
                            ProjectId = string.IsNullOrWhiteSpace(_options.ProjectId)
                                ? null
                                : _options.ProjectId
                        },
                        AppName);
                }

                _messaging = FirebaseMessaging.GetMessaging(_sharedApp);
                SetStatus("Firebase Admin SDK initialized.");
            }
            catch (Exception ex)
            {
                SetStatus($"Firebase initialization failed: {ex.Message}");
                _logger.LogError(ex, "Failed to initialize the Firebase Admin SDK.");
            }
            finally
            {
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string? ResolveCredentialsPath()
    {
        var configured = _options.CredentialsPath;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(configured);
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        return string.IsNullOrWhiteSpace(fromEnvironment)
            ? null
            : fromEnvironment;
    }

    private void SetStatus(string status)
    {
        lock (_stateLock)
        {
            _configurationStatus = status;
        }
    }
}
