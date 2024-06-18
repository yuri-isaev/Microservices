using EventBus.Base.Contracts;
using EventBus.Factory;
using EventBus.Tests.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBus.Tests;

[TestFixture]
public class EventBusTests
{
  private ServiceCollection _services = null!;
  private TestEventBusConfig _config = null!;

  [SetUp]
  public void Setup()
  {
    _services = new ServiceCollection();
    _services.AddLogging(config => config.AddConsole());
    _config = new TestEventBusConfig();
  }

  [Test]
  public void Subscribe_Event_On_RabbitMQ_Test()
  {
    _services.AddSingleton(sp => EventBusFactory.Create(_config.GetRabbitMQConfig(), sp));

    var sp = _services.BuildServiceProvider();
    var eventBus = sp.GetRequiredService<IEventBus>();

    eventBus.Subscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
    eventBus.UnSubscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
  }
}
