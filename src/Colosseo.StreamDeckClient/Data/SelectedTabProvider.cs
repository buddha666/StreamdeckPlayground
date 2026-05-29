using System;

namespace Colosseo.StreamDeckClient.Data
{
  public class SelectedTabProvider : ISelectedTabProvider
  {
    public int? SelectedBankId { get; private set; }
    public int? SelectedTabId { get; private set; }

    public event EventHandler<SelectedTabChangedEventArgs> SelectedTabChanged;

    public void SetSelectedTab(int bankId, int tabId)
    {
      SelectedBankId = bankId;
      SelectedTabId = tabId;
      SelectedTabChanged?.Invoke(this, new SelectedTabChangedEventArgs(bankId, tabId));
    }
  }
}
