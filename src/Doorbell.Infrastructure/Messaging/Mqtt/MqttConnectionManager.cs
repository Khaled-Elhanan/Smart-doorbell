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

    private string? _subscriptionTopic;

    private string? _subscribedTopic;

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

                _subscribedTopic = null;

                _logger.LogInformation(
                    "Connected to MQTT broker at {Host}:{Port}",
                    _options.Host,
                    _options.Port);
            }

            var topic = _subscriptionTopic;

            if (topic is not null &&
                _subscribedTopic != topic)
            {
                var subscribeOptions =
                    _factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(
                            filter => filter
                                .WithTopic(topic)
                                .WithAtLeastOnceQoS())
                        .Build();

                await _client.SubscribeAsync(
                    subscribeOptions,
                    cancellationToken);

                _subscribedTopic = topic;

                _logger.LogInformation(
                    "Subscribed to MQTT topic {Topic}",
                    topic);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public Task SubscribeAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        _subscriptionTopic = topic;

        return EnsureConnectedAsync(cancellationToken);
    }
}
