using System;
using System.Text;

namespace Colosseo.StreamDeckClient.UI;

public static class TextUtils
{
  public static string BreakLongWords(string text, int chunkSize)
  {
    if (string.IsNullOrWhiteSpace(text)) return "";
    text = text.Trim();

    var parts = text.Split(' ');
    for (int i = 0; i < parts.Length; i++)
    {
      parts[i] = BreakWord(parts[i], chunkSize);
    }
    return string.Join(" ", parts);
  }

  private static string BreakWord(string word, int chunkSize)
  {
    if (string.IsNullOrWhiteSpace(word)) return word;
    if (word.Length <= chunkSize) return word;

    var sb = new StringBuilder(word.Length + (word.Length / chunkSize));
    for (int i = 0; i < word.Length; i += chunkSize)
    {
      if (i > 0) sb.Append('\n');
      sb.Append(word, i, Math.Min(chunkSize, word.Length - i));
    }
    return sb.ToString();
  }

  public static string Ellipsize(string s, int maxLen)
  {
    if (s == null) return "";
    s = s.Trim();
    if (s.Length <= maxLen) return s;
    return s.Substring(0, maxLen - 1) + "…";
  }
}
