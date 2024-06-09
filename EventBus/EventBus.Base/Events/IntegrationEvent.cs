using Newtonsoft.Json;

namespace EventBus.Base.Events;

/// <summary>
/// This class is the base class for all integration events.
/// </summary>
public class IntegrationEvent
{
  [JsonProperty]
  public Guid Id { get; private set; }
  
  [JsonProperty]
  public DateTime CreatedDate { get; private set; }

  [JsonConstructor]
  public IntegrationEvent(Guid id, DateTime createdDate)
  {
    Id = id;
    CreatedDate = createdDate;
  }
  
  public IntegrationEvent()
  {
    Id = Guid.NewGuid();
    CreatedDate = DateTime.Now;
  }
}
