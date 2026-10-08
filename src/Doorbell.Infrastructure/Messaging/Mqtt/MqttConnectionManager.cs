using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt;

public sealed class MqttConnectionManager
{
    private readonly MqttClientFactory _factory;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly IMqttClient _client;
    private readonly HashSet<string> _desiredTopics =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _subscribedTopics =
        new(StringComparer.Ordinal);

    public MqttConnectionManager(
        MqttClientFactory factory,
        IOptions<MqttOptions> options,
        ILogger<MqttConnectionManager> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
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
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(_options.ClientId)
                    .WithTcpServer(
                        _options.Host,
                        _options.Port)
                    .WithCleanStart(false)
                    .WithSessionExpiryInterval(
                        (uint)_options.SessionExpirySeconds)
                    .WithKeepAlivePeriod(
                        TimeSpan.FromSeconds(30))
                    .Build();

                await _client.ConnectAsync(
                    options,
                    cancellationToken);

                _subscribedTopics.Clear();

                _logger.LogInformation(
                    "Connected to MQTT broker at {Host}:{Port}",
                    _options.Host,
                    _options.Port);
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
}
