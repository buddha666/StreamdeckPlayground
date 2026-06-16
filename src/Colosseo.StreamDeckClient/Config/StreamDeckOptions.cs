namespace Colosseo.StreamDeckClient.Config;

public sealed class StreamDeckOptions
{
  public bool Enabled { get; set; } = false;

  public string FlowSenderIdentifier { get; init; } = "showManager StreamDeck";

  public bool IsInLiveMode { get; init; } = true;

  /// <summary>When true the "following" mode layout is used: events are shown directly
  /// for the tab selected via <see cref="Colosseo.StreamDeckClient.Data.ISelectedTabProvider"/>
  /// without any bank/tab navigation.</summary>
  public StreamDeckMode Mode { get; init; } = StreamDeckMode.Following;

  /// <summary>When true the last column (x=7) is reserved for STOP-action buttons.</summary>
  public bool ShowStopButtons { get; init; } = true;

  /// <summary>When true the QuickTab column is shown next to the stop buttons (or as the last column when stop buttons are hidden).</summary>
  public bool ShowQuickTab { get; init; } = true;
}

public enum StreamDeckMode
{
  //Heh aky classy summary ten github vystruhal

  /// <summary>Classic mode with bank/tab navigation.</summary>
  Independent,
  /// <summary>Following mode: events are shown directly for the tab selected via <see cref="Colosseo.StreamDeckClient.Data.ISelectedTabProvider"/>.</summary>
  Following
}
