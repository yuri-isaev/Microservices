using EventBus.Base.Contracts;

namespace EventBus.Tests.Setup;

public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
  // The method completes the asynchronous task immediately without doing any real work.
  // This can be useful for testing or in cases where event handling has not yet been implemented.
  public Task Handle(OrderCreatedIntegrationEvent @event)
  {
    return Task.CompletedTask;
  }
}
