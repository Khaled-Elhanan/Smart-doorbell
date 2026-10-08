namespace Doorbell.Infrastructure.Messaging.Mqtt;

public static class MqttTopicFilter
{
    public static bool IsMatch(string topic, string filter)
    {
        if (string.IsNullOrEmpty(topic) ||
            string.IsNullOrEmpty(filter))
        {
            return false;
        }

        var topicParts = topic.Split('/');
        var filterParts = filter.Split('/');

        for (var i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "#")
            {
                return true;
            }

            if (i >= topicParts.Length)
            {
                return false;
            }

            if (filterParts[i] == "+")
            {
                continue;
            }

            if (!string.Equals(
                    filterParts[i],
                    topicParts[i],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return topicParts.Length == filterParts.Length;
    }
}
