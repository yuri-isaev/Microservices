using EventBus.Base.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace EventBus.Base.Events;

public abstract class BaseEventBus : IEventBus, IDisposable
{
  public readonly IServiceProvider ServiceProvider;
  public readonly IEventBusSubscriptionManager SubsManager;

  public EventBusConfig EventBusConfig { get; private set; }

  public BaseEventBus(EventBusConfig config, IServiceProvider serviceProvider)
  {
    EventBusConfig = config;
    ServiceProvider = serviceProvider;
    SubsManager = new EventBusSubscriptionManager(ProcessEventName);
  }

  /// <summary>
  /// The method processes the event name, removing the prefix and suffix if specified in the configuration.
  /// </summary>
  /// <param name="eventName"></param>
  /// <returns></returns>
  public virtual string ProcessEventName(string eventName)
  {
    if (EventBusConfig.DeleteEventPrefix)
    { 
      eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());
    }
    if (EventBusConfig.DeleteEventSuffix)
    { 
      eventName = eventName.TrimEnd(EventBusConfig.EventNameSuffix.ToArray());
    }
    return eventName;
  }

  /// <summary>
  /// The method returns the subscription name for the specified event.
  /// </summary>
  /// <param name="eventName"></param>
  /// <returns></returns>
  public virtual string GetSubName(string eventName)
  {
    return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
  }

  /// <summary>
  /// The method frees resources by clearing the configuration and subscription manager.
  /// </summary>
  public virtual void Dispose()
  {
    EventBusConfig = null!;
    SubsManager.Clear();
  }

  /// <summary>
  /// The method handles the event by calling the appropriate handlers.
  /// </summary>
  /// <param name="eventName"></param>
  /// <param name="message"></param>
  public async Task<bool> ProcessEvent(string eventName, string message)
  {
    eventName = ProcessEventName(eventName);
    var processed = false;

    if (SubsManager.HasSubscriptionsForEvent(eventName))
    {
      var subscriptions = SubsManager.GetHandlersForEvent(eventName);

      using (ServiceProvider.CreateScope())
      {
        foreach (var subscription in subscriptions)
        {
          var handler = ServiceProvider.GetService(subscription.HandlerType);
          if (handler == null) continue;

          var eventType = SubsManager.GetEventTypeByName($"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
          var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
          var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

          await (Task) concreteType
            .GetMethod("Handle")!
            .Invoke(handler, new[] {integrationEvent})!;
        }
      }

      processed = true;
    }

    return processed;
  }

  public abstract void Publish(IntegrationEvent @event);

  public abstract void Subscribe<TEvent, THandler>()
    where TEvent : IntegrationEvent where THandler : IIntegrationEventHandler<TEvent>;

  public abstract void UnSubscribe<TEvent, THandler>()
    where TEvent : IntegrationEvent where THandler : IIntegrationEventHandler<TEvent>;
}
