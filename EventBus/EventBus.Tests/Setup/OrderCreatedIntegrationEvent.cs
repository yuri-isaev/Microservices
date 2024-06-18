using EventBus.Base.Events;

namespace EventBus.Tests.Setup;

public class OrderCreatedIntegrationEvent : IntegrationEvent
{
  // The new property hides the `Id` property from the base class.
  public new int Id { get; set; }

  public OrderCreatedIntegrationEvent(int id)
  {
    Id = id;
  }
}
