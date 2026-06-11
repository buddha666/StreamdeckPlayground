using System;

namespace Colosseo.StreamDeckClient.Device;

/// <summary>
/// Describes the physical key-grid dimensions of the connected Stream Deck device and
/// derives all layout-dependent key positions from those dimensions.
/// </summary>
public sealed class DeviceLayout
{
    /// <summary>Default 8×4 layout (Stream Deck XL).</summary>
    public static readonly DeviceLayout Default = new DeviceLayout(8, 4);

    public int Columns { get; }
    public int Rows { get; }

    /// <summary>Total number of keys (Columns × Rows).</summary>
    public int TotalKeys => Columns * Rows;

    /// <summary>True when the device has exactly 3 rows (e.g. regular Stream Deck 5×3).</summary>
    public bool IsThreeRow => Rows == 3;

    // ---- navigation-column key positions (ShowNavigationControls pages) ----

    /// <summary>Last column, row 0 — UP scroll key.</summary>
    public int KeyScrollUp => Columns - 1;

    /// <summary>Last column, row 1 — DOWN scroll key.</summary>
    public int KeyScrollDown => 2 * Columns - 1;

    /// <summary>Last column, row 2 — INFO key.</summary>
    public int KeyInfo => 3 * Columns - 1;

    // ---- back-button positions ----

    /// <summary>Bottom-left key index: (Rows-1) × Columns + 0.</summary>
    public int KeyBackBottomLeft => (Rows - 1) * Columns;

    /// <summary>Bottom-right key index (default nav-column back position).</summary>
    public int KeyBackDefault => Columns * Rows - 1;

    /// <param name="columns">Number of key columns (≥ 1).</param>
    /// <param name="rows">Number of key rows (≥ 3 — devices with fewer rows are not supported).</param>
    public DeviceLayout(int columns, int rows)
    {
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns), "Must be at least 1.");
        if (rows < 3) throw new ArgumentOutOfRangeException(nameof(rows), "Stream Deck must have at least 3 rows.");
        Columns = columns;
        Rows = rows;
    }
}
