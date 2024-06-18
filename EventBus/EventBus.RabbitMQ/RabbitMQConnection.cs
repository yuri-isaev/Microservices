using System.Net.Sockets;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ;

/// <summary>
/// RabbitMQConnection is a class that manages the connection to RabbitMQ,
/// including retry logic and event handling for connection issues.
/// </summary>
public class RabbitMQConnection : IDisposable
{
  private readonly IConnectionFactory _connectionFactory;
  private readonly int _retryCount;
  
  private IConnection _connection = null!;
  private bool _disposed;
  private object _lockObject = new();

  /// <summary>
  /// Indicates whether the connection is currently open and connected.
  /// </summary>
  public bool IsConnected => _connection != null && _connection.IsOpen;

  public RabbitMQConnection(IConnectionFactory connectionFactory, int retryCount = 5)
  {
    _connectionFactory = connectionFactory;
    _retryCount = retryCount;
  }

  /// <summary>
  /// Creates a new channel (model) for interacting with RabbitMQ.
  /// </summary>
  /// <returns>An IModel representing the channel.</returns>
  public IModel CreateChannel()
  {
    return _connection.CreateModel();
  }

  /// <summary>
  /// Attempts to connect to RabbitMQ with retry logic.
  /// </summary>
  /// <returns>True if the connection is successful, otherwise false.</returns>
  public bool TryConnect()
  {
    // This block of code is used to provide thread safety.
    // It ensures that only one thread can execute code inside a `lock` block at any given time.
    // This prevents problems associated with multiple threads accessing shared resources at the same time.
    lock (_lockObject)
    {
      // Create a retry policy
      var policy = Policy.Handle<SocketException>()
        .Or<BrokerUnreachableException>()
        .WaitAndRetry(_retryCount, GetRetryInterval, (ex, time) => {});

      // This is the execution of a connection operation according to a previously defined policy.
      // If exceptions defined in the policy occur, the operation will be retried.
      policy.Execute(() => _connection = _connectionFactory.CreateConnection());

      // If the connection is successful, it adds handlers for the various connection events and returns `true`
      // Otherwise, the method returns `false`.
      if (IsConnected)
      {
        // Connection subscribe to events
        _connection.ConnectionShutdown += ConnectionShutdown!;
        _connection.CallbackException  += ConnectionCallbackException!;
        _connection.ConnectionBlocked  += ConnectionBlocked!;
        return true;
      }

      return false;
    }
  }
  
  /// <summary>
  /// Gets the retry interval for connection attempts.
  /// </summary>
  private TimeSpan GetRetryInterval(int retryAttempt)
  {
    return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
  }
  
  /// <summary>
  /// Handles the event when the connection is blocked.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="e">The event arguments containing the details of the connection block.</param>
  private void ConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
  {
    // We check whether the object has already been disposed. If yes, we stop executing the method.
    if (_disposed) return;
    
    // If the object is still alive, we try to restore the connection.
    TryConnect();
  }

  /// <summary>
  /// Handles the event when a callback exception occurs.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="e">The event arguments containing the details of the callback exception.</param>
  private void ConnectionCallbackException(object sender, CallbackExceptionEventArgs e)
  {
    if (_disposed) return;
    TryConnect();
  }

  /// <summary>
  /// Handles the event when the connection is shut down.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="e">The event arguments containing the details of the shutdown.</param>
  private void ConnectionShutdown(object sender, ShutdownEventArgs e)
  {
    if (_disposed) return;
    TryConnect();
  }

  /// <summary>
  /// Disposes the connection and releases resources.
  /// </summary>
  public void Dispose()
  {
    _disposed = true;
    _connection.Dispose();
  }
}
