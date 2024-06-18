using System.Text;
using EventBus.Base;
using EventBus.Base.Events;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EventBus.AzureServiceBus;

/// <summary>
/// The class is a concrete implementation of the abstract BaseEventBus class and
/// provides functionality for publications, subscriptions and unsubscribes from events through the Azure Service Bus.
/// </summary>
public class EventBusService : BaseEventBus
{
  private ILogger _logger;
  private ITopicClient _topicClient;
  private ManagementClient _managementClient;

  public EventBusService(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
  {
    _logger = serviceProvider.GetRequiredService<ILogger<EventBusService>>();
    _managementClient = new ManagementClient(config.EventBusConnectionString);
    _topicClient = CreateTopicClient();
  }

  /// <summary>
  /// The method creates a topic client if it is not already created or closed.
  /// It checks if the topic exists and creates it if it doesn't.
  /// </summary>
  private ITopicClient CreateTopicClient()
  {
    var topic = EventBusConfig.DefaultTopicName;
    
    if (_topicClient == null || _topicClient.IsClosedOrClosing)
    {
      _topicClient = new TopicClient(EventBusConfig.EventBusConnectionString, topic, RetryPolicy.Default);
    }

    // Ensure that topic already exists
    if (!_managementClient.TopicExistsAsync(topic).GetAwaiter().GetResult())
    {
      _managementClient.CreateTopicAsync(topic).GetAwaiter().GetResult();
    }

    return _topicClient;
  }

  /// <summary>
  /// The method publishes an event to a topic.
  /// It serializes the event into a JSON string, encodes JSON string into a byte array, and sends a message to the topic.
  /// </summary>
  /// <param name="event">The event to be published.</param>
  public override void Publish(IntegrationEvent @event)
  {
    var eventName = @event.GetType().Name;
    eventName = ProcessEventName(eventName);

    var eventStr = JsonConvert.SerializeObject(@event);
    var bodyArr = Encoding.UTF8.GetBytes(eventStr);

    var message = new Message()
    {
      MessageId = Guid.NewGuid().ToString(),
      Body = bodyArr,
      Label = eventName
    };

    _topicClient.SendAsync(message).GetAwaiter().GetResult();
  }

  /// <summary>
  /// The method subscribes to the event.
  /// It checks if a subscription to the event exists, and if not, creates a subscription client
  /// and registers a message handler.
  /// </summary>
  /// <typeparam name="TEvent"></typeparam>
  /// <typeparam name="THandler"></typeparam>
  public override void Subscribe<TEvent, THandler>()
  {
    var eventName = typeof(TEvent).Name;
    eventName = ProcessEventName(eventName);

    if (!SubsManager.HasSubscriptionsForEvent(eventName))
    {
      var subscriptionClient = CreateSubscriptionClientIfNotExists(eventName);
      RegisterSubscriptionClientMessageHandler(subscriptionClient);
    }

    _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(THandler).Name);
    SubsManager.AddSubscription<TEvent, THandler>();
  }

  /// <summary>
  /// The method unsubscribes from the event.
  /// It removes the subscription rule for the specified event.
  /// </summary>
  /// <typeparam name="TEvent">The type of the event to unsubscribe from.</typeparam>
  /// <typeparam name="THandler">The type of the handler that was handling the event.</typeparam>
  public override void UnSubscribe<TEvent, THandler>()
  {
    var eventName = typeof(TEvent).Name;

    try
    {
      var subscriptionClient = CreateSubscriptionClient(eventName);
      subscriptionClient.RemoveRuleAsync(eventName).GetAwaiter().GetResult();
    }
    catch (MessagingEntityNotFoundException)
    {
      _logger.LogWarning("The messaging entity {eventName} Could not be found.", eventName);
    }

    _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
    SubsManager.RemoveSubscription<TEvent, THandler>();
  }

  /// <summary>
  /// The method registers a message handler for the subscription client.
  /// It processes messages and terminates them if processing is successful.
  /// </summary>
  /// <param name="subscriptionClient">
  /// The subscription client to register the message handler for.
  /// </param>
  private void RegisterSubscriptionClientMessageHandler(ISubscriptionClient subscriptionClient)
  {
    subscriptionClient.RegisterMessageHandler(async (message, _) =>
      {
        var eventName = $"{message.Label}";
        var messageData = Encoding.UTF8.GetString(message.Body);

        // Complete the message so that it is not received again.
        if (await ProcessEvent(ProcessEventName(eventName), messageData))
        {
          await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
        }
      },
      new MessageHandlerOptions(ExceptionReceivedHandler)
      {
        MaxConcurrentCalls = 10,
        AutoComplete = false
      }
    );
  }

  /// <summary>
  /// The method handles exceptions that occur when processing messages.
  /// </summary>
  /// <param name="exceptionReceivedEventArgs"></param>
  /// <returns></returns>
  private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
  {
    var ex = exceptionReceivedEventArgs.Exception;
    var context = exceptionReceivedEventArgs.ExceptionReceivedContext;

    _logger.LogError(ex, "ERROR handling message: {ExceptionMessage} - Context: {@ExceptionContext}", ex.Message,
      context);

    return Task.CompletedTask;
  }
  
  /// <summary>
  /// The method creates a subscription client if it does not exist.
  /// It checks if the subscription exists and creates one if it doesn't.
  /// It removes the default rule and creates a rule for the event if it doesn't exist.
  /// </summary>
  /// <param name="eventName">The name of the event for which the subscription client is created.</param>
  /// <returns>The created or existing subscription client.</returns>
  private ISubscriptionClient CreateSubscriptionClientIfNotExists(String eventName)
  {
    // Create a subscription client for the specified event.
    var subClient = CreateSubscriptionClient(eventName);

    // Check if the subscription exists.
    var exists = _managementClient
      .SubscriptionExistsAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName))
      .GetAwaiter().GetResult();

    // If the subscription does not exist, create it.
    if (!exists)
    {
      _managementClient
        .CreateSubscriptionAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName))
        .GetAwaiter()
        .GetResult();
      
      // Remove the default rule from the subscription.
      RemoveDefaultRule(subClient);
    }

    // Create a rule for the event if it doesn't exist.
    CreateRuleIfNotExists(ProcessEventName(eventName), subClient);

    // Return the subscription client.
    return subClient;
  }

  /// <summary>
  /// The method сreates a rule for an event if it does not exist.
  /// </summary>
  /// <param name="eventName"></param>
  /// <param name="subscriptionClient"></param>
  private void CreateRuleIfNotExists(string eventName, ISubscriptionClient subscriptionClient)
  {
    bool ruleExits;

    try
    {
      var rule = _managementClient
        .GetRuleAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName), eventName)
        .GetAwaiter()
        .GetResult();
      ruleExits = rule != null;
    }
    catch (MessagingEntityNotFoundException)
    {
      // Azure Management Client doesn't have RuleExists method
      ruleExits = false;
    }

    if (!ruleExits)
    {
      subscriptionClient
        .AddRuleAsync(new RuleDescription {Filter = new CorrelationFilter {Label = eventName}, Name = eventName})
        .GetAwaiter()
        .GetResult();
    }
  }

  /// <summary>
  /// The method removes the default rule for the subscription client.
  /// </summary>
  /// <param name="subscriptionClient"></param>
  private void RemoveDefaultRule(SubscriptionClient subscriptionClient)
  {
    try
    {
      subscriptionClient
        .RemoveRuleAsync(RuleDescription.DefaultRuleName)
        .GetAwaiter()
        .GetResult();
    }
    catch (MessagingEntityNotFoundException)
    {
      _logger.LogWarning("The messaging entity {DefaultRuleName} Could not be found.", RuleDescription.DefaultRuleName);
    }
  }

  /// <summary>
  /// The method creates a subscription client for the specified event.
  /// </summary>
  /// <param name="eventName"></param>
  /// <returns></returns>
  private SubscriptionClient CreateSubscriptionClient(string eventName)
  {
    return new SubscriptionClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName, GetSubName(eventName));
  }

  /// <summary>
  /// The method frees resources by closing topic and management clients.
  /// </summary>
  public override void Dispose()
  {
    base.Dispose();
    _topicClient.CloseAsync().GetAwaiter().GetResult();
    _managementClient.CloseAsync().GetAwaiter().GetResult();
    _topicClient = null!;
    _managementClient = null!;
  }
}
