namespace EventBus.Base;

/// <summary>
/// This class represents event subscription information.
/// It contains the HandlerType property, which stores the type of the event handler.
/// The SubscriptionInfo(Type handlerType) constructor accepts a handler type and initializes a SubscriptionInfo object.
/// </summary>
public class SubscriptionInfo
{
  public Type HandlerType { get; }

  public SubscriptionInfo(Type handlerType)
  {
    HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
  }

  public static SubscriptionInfo Typed(Type handlerType)
  {
    return new SubscriptionInfo(handlerType);
  }
}
