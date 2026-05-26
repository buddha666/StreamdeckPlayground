using System.Drawing;

namespace Colosseo.StreamDeckClient.Rendering;

public static class ColorUtil
{
  public static Color FromFlowColor(int? flowColor, Color fallback)
      => flowColor.HasValue ? Color.FromArgb(flowColor.Value) : fallback;
}
