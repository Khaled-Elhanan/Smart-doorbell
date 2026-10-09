using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt;

public sealed class MqttConnectionManager : IAsyncDisposable
{
    private const int MaxConnectAttempts = 5;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(8);

    private readonly MqttClientFactory _factory;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly string _clientId;
    private readonly IMqttClient _client;
    private readonly HashSet<string> _desiredTopics =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _subscribedTopics =
        new(StringComparer.Ordinal);
    private int _failedAttempts;

    public MqttConnectionManager(
        MqttClientFactory factory,
        IOptions<MqttOptions> options,
        ILogger<MqttConnectionManager> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
        _clientId = string.IsNullOrWhiteSpace(_options.ClientId)
            ? "doorbell-backend-" + Guid.NewGuid().ToString("N")[..12]
            : _options.ClientId;
        _client = factory.CreateMqttClient();
    }

    public IMqttClient Client => _client;

    public bool IsConnected => _client.IsConnected;

    public async Task EnsureConnectedAsync(
        CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (!_client.IsConnected)
            {
                await ConnectWithBackoffAsync(cancellationToken);

                _subscribedTopics.Clear();

                _logger.LogInformation(
                    "Connected to MQTT broker at {Host}:{Port} (ClientId {ClientId})",
                    _options.Host,
                    _options.Port,
                    _clientId);
            }

            var topicsToSubscribe = _desiredTopics
                .Where(topic => !_subscribedTopics.Contains(topic))
                .ToList();

            if (topicsToSubscribe.Count > 0)
            {
                var subscribeOptions =
                    _factory.CreateSubscribeOptionsBuilder();

                foreach (var topic in topicsToSubscribe)
                {
                    subscribeOptions = subscribeOptions
                        .WithTopicFilter(
                            filter => filter
                                .WithTopic(topic)
                                .WithAtLeastOnceQoS());
                }

                await _client.SubscribeAsync(
                    subscribeOptions.Build(),
                    cancellationToken);

                foreach (var topic in topicsToSubscribe)
                {
                    _subscribedTopics.Add(topic);

                    _logger.LogInformation(
                        "Subscribed to MQTT topic {Topic}",
                        topic);
                }
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SubscribeAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            _desiredTopics.Add(topic);
        }
        finally
        {
            _connectionLock.Release();
        }

        await EnsureConnectedAsync(cancellationToken);
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_clientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithCleanStart(false)
            .WithSessionExpiryInterval(
                (uint)Math.Max(0, _options.SessionExpirySeconds))
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder = builder.WithCredentials(
                _options.Username,
                _options.Password ?? string.Empty);
        }

        if (_options.UseTls)
        {
            builder = builder.WithTlsOptions(
                tls => tls.WithSslProtocols(SslProtocols.Tls12));
        }

        return builder.Build();
    }

    private async Task ConnectWithBackoffAsync(
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                await _client.ConnectAsync(
                    BuildClientOptions(),
                    cancellationToken);

                _failedAttempts = 0;

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                _failedAttempts++;

                if (attempt >= MaxConnectAttempts)
                {
                    _logger.LogError(
                        ex,
                        "MQTT connect to {Host}:{Port} failed after {Attempts} attempts.",
                        _options.Host,
                        _options.Port,
                        attempt);

                    throw new InvalidOperationException(
                        $"MQTT connect to {_options.Host}:{_options.Port} failed after {attempt} attempts.",
                        ex);
                }

                var delay = ComputeBackoffDelay(_failedAttempts);

                _logger.LogWarning(
                    ex,
                    "MQTT connect to {Host}:{Port} failed (attempt {Attempt}); retrying in {DelayMs}ms.",
                    _options.Host,
                    _options.Port,
                    attempt,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static TimeSpan ComputeBackoffDelay(int failedAttempts)
    {
        // 1s, 2s, 4s, 8s, capped at 8s, with +/-20% jitter.
        var exponent = Math.Min(failedAttempts - 1, 3);
        var baseMs =
            BaseRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);

        var cappedMs = Math.Min(baseMs, MaxRetryDelay.TotalMilliseconds);
        var jitter = (Random.Shared.NextDouble() * 0.4) - 0.2;

        return TimeSpan.FromMilliseconds(
            Math.Max(1, cappedMs * (1 + jitter)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync(
                    new MqttClientDisconnectOptions(),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error while disconnecting MQTT client during shutdown.");
            }
        }

        _client.Dispose();
        _connectionLock.Dispose();
    }
}