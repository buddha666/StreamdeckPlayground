using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Power;

namespace Colosseo.StreamDeckClient.Data
{
  public interface ISelectedTabProvider
  {
    int? SelectedBankId { get; }
    int? SelectedTabId { get; }

    event EventHandler<SelectedTabChangedEventArgs> SelectedTabChanged;

    void SetSelectedTab(int bankId, int tabId);
  }

  public class SelectedTabChangedEventArgs : EventArgs
  {
    public int BankId { get; }
    public int TabId { get; }
    public SelectedTabChangedEventArgs(int bankId, int tabId) { BankId = bankId; TabId = tabId; }
  }
}
