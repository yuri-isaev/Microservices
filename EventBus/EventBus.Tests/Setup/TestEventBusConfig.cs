using EventBus.Base;
using RabbitMQ.Client;

namespace EventBus.Tests.Setup;

public class TestEventBusConfig
{
  internal EventBusConfig GetRabbitMQConfig()
  {
    return new EventBusConfig()
    {
      ConnectionRetryCount = 5,
      SubscriberClientAppName = "EventBus.Tests",
      DefaultTopicName = "EventBus",
      EventBusType = EventBusType.RabbitMQ, // or AzureServiceBus
      EventNameSuffix = "IntegrationEvent",
      Connection = new ConnectionFactory()
      { 
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest"
      }
    };
  }
}
