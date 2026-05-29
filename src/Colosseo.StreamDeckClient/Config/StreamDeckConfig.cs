namespace Colosseo.StreamDeckClient.Config;

public sealed class StreamDeckClientConfig
{
  public string FlowSenderIdentifier { get; init; } = "showManager StreamDeck";

  public bool IsInLiveMode { get; init; } = false;

  /// <summary>When true the "following" mode layout is used: events are shown directly
  /// for the tab selected via <see cref="Colosseo.StreamDeckClient.Data.ISelectedTabProvider"/>
  /// without any bank/tab navigation.</summary>
  public bool StreamDeckModeIsFollowing { get; init; } = false;

  /// <summary>When true the last column (x=7) is reserved for STOP-action buttons.</summary>
  public bool StreamDeckShowStopButtons { get; init; } = true;

  /// <summary>When true the QuickTab column is shown next to the stop buttons (or as the last column when stop buttons are hidden).</summary>
  public bool StreamDeckShowQuickTab { get; init; } = true;
}
