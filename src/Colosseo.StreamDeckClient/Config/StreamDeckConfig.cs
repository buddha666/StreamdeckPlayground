namespace Colosseo.StreamDeckClient.Config;

public sealed class StreamDeckClientConfig
{
  public string FlowSenderIdentifier { get; init; } = "showManager StreamDeck";

  public bool IsInLiveMode { get; init; } = false;
}
