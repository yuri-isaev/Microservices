using EventBus.Base.Contracts;
using EventBus.Base.Events;

namespace EventBus.Base;

/// <summary>
/// This class is responsible for managing subscriptions to message bus events.
/// Inside it there is a _handlers dictionary that maps the event name to a list of SubscriptionInfo objects.
/// Each SubscriptionInfo represents information about subscribers to a given event.
/// </summary>
public class EventBusSubscriptionManager : IEventBusSubscriptionManager
{
  readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
  readonly List<Type> _eventTypes;
  public event EventHandler<string> OnEventRemoved = null!;
  public Func<string, string> EventNameGetter;

  public EventBusSubscriptionManager(Func<string, string> eventNameGetter)
  {
    _handlers = new Dictionary<string, List<SubscriptionInfo>>();
    _eventTypes = new List<Type>();
    EventNameGetter = eventNameGetter;
  }

  public bool IsEmpty => !_handlers.Keys.Any();
  public void Clear() => _handlers.Clear();

  public void AddSubscription<TEvent, THandler>()
    where TEvent : IntegrationEvent
    where THandler : IIntegrationEventHandler<TEvent>
  {
    var eventName = GetEventKey<TEvent>();
    AddSubscription(typeof(THandler), eventName);

    if (!_eventTypes.Contains(typeof(TEvent)))
    {
      _eventTypes.Add(typeof(TEvent));
    }
  }

  private void AddSubscription(Type handlerType, string eventName)
  {
    if (!HasSubscriptionsForEvent(eventName))
    {
      _handlers.Add(eventName, new List<SubscriptionInfo>());
    }

    if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
    {
      throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'",
        nameof(handlerType));
    }

    _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
  }

  public void RemoveSubscription<TEvent, THandler>()
    where THandler : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
  {
    var handlerToRemove = FindSubscriptionToRemove<TEvent, THandler>();
    var eventName = GetEventKey<TEvent>();
    RemoveHandler(eventName, handlerToRemove);
  }

  private void RemoveHandler(string eventName, SubscriptionInfo subsToRemove)
  {
    if (true)
    {
      _handlers[eventName].Remove(subsToRemove);

      if (!_handlers[eventName].Any())
      {
        _handlers.Remove(eventName);
        var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
        if (eventType != null)
        {
          _eventTypes.Remove(eventType);
        }

        RaiseOnEventRemoved(eventName);
      }
    }
  }

  public IEnumerable<SubscriptionInfo> GetHandlersForEvent<TEvent>() where TEvent : IntegrationEvent
  {
    var key = GetEventKey<TEvent>();
    return GetHandlersForEvent(key);
  }

  public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];

  private void RaiseOnEventRemoved(string eventName)
  {
    var handler = OnEventRemoved;
    handler(this, eventName);
  }

  private SubscriptionInfo FindSubscriptionToRemove<TEvent, THandler>()
    where TEvent : IntegrationEvent
    where THandler : IIntegrationEventHandler<TEvent>
  {
    var eventName = GetEventKey<TEvent>();
    return FindSubscriptionToRemove(eventName, typeof(THandler));
  }

  private SubscriptionInfo FindSubscriptionToRemove(string eventName, Type handlerType)
  {
    if (!HasSubscriptionsForEvent(eventName))
    {
      return null!;
    }

    return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType)!;
  }

  public bool HasSubscriptionsForEvent<TEvent>() where TEvent : IntegrationEvent
  {
    var key = GetEventKey<TEvent>();
    return HasSubscriptionsForEvent(key);
  }

  public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

  public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName)!;

  public string GetEventKey<TEvent>() where TEvent : IntegrationEvent
  {
    string eventName = typeof(TEvent).Name;
    return EventNameGetter(eventName);
  }
}
