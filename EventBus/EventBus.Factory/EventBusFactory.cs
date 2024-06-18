using EventBus.AzureServiceBus;
using EventBus.Base;
using EventBus.Base.Contracts;
using EventBus.RabbitMQ;

namespace EventBus.Factory;

public static class EventBusFactory
{
  public static IEventBus Create(EventBusConfig config, IServiceProvider serviceProvider)
  {
    return config.EventBusType switch
    {
      EventBusType
        .AzureServiceBus => new EventBusService(config, serviceProvider),
        _ => new EventBusRabbitMQ(config, serviceProvider)
    };
  }
}
