using System.Net.Sockets;
using System.Text;
using EventBus.Base;
using EventBus.Base.Events;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ;

/// <summary>
/// EventBusRabbitMQ is a concrete implementation of the BaseEventBus class that uses RabbitMQ for event-based communication.
/// This class handles the connection to RabbitMQ,
/// publishing events,
/// subscribing to events,
/// managing the lifecycle of the consumer channel.
/// </summary>
public class EventBusRabbitMQ : BaseEventBus
{
  private readonly IConnectionFactory _connectionFactory; 
  private readonly IModel _consumerChannel;
  
  private RabbitMQConnection persistentConnection;
  private static readonly int ConnectionRetryCount = 5;

  /// <summary>
  /// Initializes a new instance of the EventBusRabbitMQ class.
  /// </summary>
  /// <param name="config">Configuration settings for the event bus.</param>
  /// <param name="provider">Service provider for dependency injection.</param>
  public EventBusRabbitMQ(EventBusConfig config, IServiceProvider provider) : base(config, provider)
  {
    var connection = EventBusConfig.Connection;
    
    if (connection != null)
    {
      if (connection is ConnectionFactory)
      {
        _connectionFactory = (connection as ConnectionFactory)!;
      }
      else
      {
        var connJson = JsonConvert.SerializeObject(connection, new JsonSerializerSettings()
        {
          ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });

        _connectionFactory = JsonConvert.DeserializeObject<ConnectionFactory>(connJson)!;
      }
    }
    else
    {
      _connectionFactory = new ConnectionFactory();
    }
    // Creates a stable connection
    persistentConnection = new RabbitMQConnection(_connectionFactory, config.ConnectionRetryCount);
    // Creates a сonsumer channel
    _consumerChannel = CreateConsumerChannel();
    // Subscription to the `OnEventRemoved` event of the subscription manager (`SubsManager`).
    // When the event is removed, the `SubsManagerOnEventRemoved` method will be called.
    SubsManager.OnEventRemoved += SubsManagerOnEventRemoved!;
  }
  
  /// <summary>
  /// Handles the event when a subscription is removed.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="eventName">The name of the event.</param>
  private void SubsManagerOnEventRemoved(object sender, string eventName)
  {
    // Converts the event name to the desired format using the ProcessEventName method.
    eventName = ProcessEventName(eventName);

    // Checks whether a persistent connection is connected. If not, it tries to connect.
    if (!persistentConnection.IsConnected) persistentConnection.TryConnect();
    
    // Unlinks the queue from exchange in RabbitMQ.
    _consumerChannel.QueueUnbind(queue: eventName, exchange: EventBusConfig.DefaultTopicName, routingKey: eventName);

    // Checks if the subscription manager is empty. If yes, closes the consumer channel.
    if (SubsManager.IsEmpty) _consumerChannel.Close();
  }

  /// <summary>
  /// Publishes an integration event to the RabbitMQ exchange.
  /// </summary>
  /// <param name="event">The integration event to publish.</param>
  public override void Publish(IntegrationEvent @event)
  {
    if (!persistentConnection.IsConnected) persistentConnection.TryConnect();
    
    var policy = Policy.Handle<BrokerUnreachableException>()
      .Or<SocketException>()
      .WaitAndRetry(ConnectionRetryCount, GetRetryInterval, (ex, time) => {});
    
    var eventName = @event.GetType().Name;
    eventName = ProcessEventName(eventName);

    // Ensure exchange exists while publishing
    _consumerChannel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");

    var message = JsonConvert.SerializeObject(@event);
    var body = Encoding.UTF8.GetBytes(message);

    policy.Execute(() =>
    {
      var properties = _consumerChannel.CreateBasicProperties();
      properties.DeliveryMode = 2;

      _consumerChannel.BasicPublish(
        exchange: EventBusConfig.DefaultTopicName,
        routingKey: eventName,
        mandatory: true,
        basicProperties: properties,
        body: body);
    });
  }
  
  /// <summary>
  /// Gets the retry interval for connection attempts.
  /// </summary>
  /// <param name="retryAttempt">The current retry attempt number.</param>
  /// <returns>A TimeSpan representing the retry interval.</returns>
  private TimeSpan GetRetryInterval(int retryAttempt)
  {
    return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
  }
  
  /// <summary>
  /// Subscribes to an event with a specified event handler.
  /// </summary>
  public override void Subscribe<TEvent,THandler>()
  {
    var eventName = typeof(TEvent).Name;
    eventName = ProcessEventName(eventName);

    if (!SubsManager.HasSubscriptionsForEvent(eventName))
    {
      if (!persistentConnection.IsConnected) persistentConnection.TryConnect();

      _consumerChannel.QueueDeclare(
        queue: GetSubName(eventName), // Ensure queue exists while consuming
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null
      );

      _consumerChannel.QueueBind(
        queue: GetSubName(eventName),
        exchange: EventBusConfig.DefaultTopicName,
        routingKey: eventName
      );
    }

    SubsManager.AddSubscription<TEvent,THandler>();
    StartBasicConsume(eventName);
  }

  /// <summary>
  /// Unsubscribes from an event with a specified event handler.
  /// </summary>
  public override void UnSubscribe<TEvent,THandler>()
  {
    SubsManager.RemoveSubscription<TEvent,THandler>();
  }
  
  /// <summary>
  /// Creates a consumer channel for RabbitMQ.
  /// </summary>
  /// <returns>An IModel representing the consumer channel.</returns>
  private IModel CreateConsumerChannel()
  {
    if (!persistentConnection.IsConnected) persistentConnection.TryConnect();
    
    var channel = persistentConnection.CreateChannel();
    channel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");

    return channel;
  }

  /// <summary>
  /// Starts the basic consumption of messages from the RabbitMQ queue.
  /// </summary>
  /// <param name="eventName">The name of the event to consume.</param>
  private void StartBasicConsume(string eventName)
  {
    if (_consumerChannel != null)
    {
      var consumer = new EventingBasicConsumer(_consumerChannel);
      consumer.Received += ConsumerReceived!;

      _consumerChannel.BasicConsume(queue: GetSubName(eventName), autoAck: false, consumer: consumer);
    }
  }

  /// <summary>
  /// Handles the receipt of a message from the RabbitMQ queue.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="eventArgs">The event arguments containing the message details.</param>
  private async void ConsumerReceived(object sender, BasicDeliverEventArgs eventArgs)
  {
    var eventName = eventArgs.RoutingKey;
    eventName = ProcessEventName(eventName);
    var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

    try
    {
      await ProcessEvent(eventName, message);
    }
    catch (Exception)
    {
      // log
    }

    _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
  }
}
