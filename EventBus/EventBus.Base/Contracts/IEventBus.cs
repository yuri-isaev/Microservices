using EventBus.Base.Events;

namespace EventBus.Base.Contracts;

public interface IEventBus
{
  void Publish(IntegrationEvent @event);

  void Subscribe<TEvent, THandler>()
    where TEvent : IntegrationEvent
    where THandler : IIntegrationEventHandler<TEvent>;

  void UnSubscribe<TEvent, THandler>()
    where TEvent : IntegrationEvent
    where THandler : IIntegrationEventHandler<TEvent>;
}
