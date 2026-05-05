namespace Alicraft2.Data;

/// <summary>
/// Shipping-fee tiers based on the customer's province.
/// Origin: Karangalan Village, Brgy. San Isidro, Cainta, Rizal.
/// We only deliver to Luzon — provinces outside this list are blocked at
/// checkout. Fees are flat per zone (no per-km pricing) so customers see a
/// stable amount that's easy to reason about.
///
/// Rates are anchored to LBC Express's published Small Kilobox (3kg) rate
/// card (a realistic envelope for a packed lithophane frame or batch of
/// keychains): ₱160 within Metro Manila, ₱190 NCR↔provincial Luzon. The
/// extra zones add modest surcharges for the far/island provinces so the
/// customer-facing fee reflects real-world distance even though LBC itself
/// uses a flat provincial rate.
/// </summary>
public static class ShippingZones
{
    public enum Zone
    {
        /// <summary>Metro Manila + Rizal (store's own province).</summary>
        Local,
        /// <summary>CALABARZON neighbours and Central-Luzon-South.</summary>
        NearLuzon,
        /// <summary>Central / north-central Luzon and CAR-south.</summary>
        CentralLuzon,
        /// <summary>Far-north Luzon (Cagayan Valley + island provinces).</summary>
        FarNorthLuzon,
        /// <summary>Bicol Region (south end of Luzon).</summary>
        Bicol,
    }

    private static readonly Dictionary<string, Zone> ProvinceZone = new()
    {
        // Local
        ["Metro Manila"]     = Zone.Local,
        ["Rizal"]            = Zone.Local,

        // Near Luzon (adjacent)
        ["Cavite"]           = Zone.NearLuzon,
        ["Laguna"]           = Zone.NearLuzon,
        ["Bulacan"]          = Zone.NearLuzon,
        ["Batangas"]         = Zone.NearLuzon,
        ["Quezon"]           = Zone.NearLuzon,
        ["Pampanga"]         = Zone.NearLuzon,
        ["Bataan"]           = Zone.NearLuzon,

        // Central Luzon and adjacent CAR / Region I
        ["Tarlac"]           = Zone.CentralLuzon,
        ["Zambales"]         = Zone.CentralLuzon,
        ["Nueva Ecija"]      = Zone.CentralLuzon,
        ["Aurora"]           = Zone.CentralLuzon,
        ["Pangasinan"]       = Zone.CentralLuzon,
        ["La Union"]         = Zone.CentralLuzon,
        ["Nueva Vizcaya"]    = Zone.CentralLuzon,
        ["Quirino"]          = Zone.CentralLuzon,
        ["Benguet"]          = Zone.CentralLuzon,
        ["Abra"]             = Zone.CentralLuzon,

        // Far north Luzon (Cagayan Valley + Batanes)
        ["Cagayan"]          = Zone.FarNorthLuzon,
        ["Isabela"]          = Zone.FarNorthLuzon,
        ["Apayao"]           = Zone.FarNorthLuzon,
        ["Batanes"]          = Zone.FarNorthLuzon,

        // Bicol
        ["Albay"]            = Zone.Bicol,
        ["Camarines Norte"]  = Zone.Bicol,
        ["Camarines Sur"]    = Zone.Bicol,
        ["Catanduanes"]      = Zone.Bicol,
        ["Sorsogon"]         = Zone.Bicol,
    };

    // Fees anchored to LBC's Small Kilobox (3kg) rate card:
    //   Within Metro Manila ............... ₱160
    //   NCR ↔ provincial Luzon ............ ₱190
    // Other tiers add modest surcharges for far/island provinces.
    private static readonly Dictionary<Zone, (decimal Fee, string Label)> ZoneInfo = new()
    {
        [Zone.Local]          = (160m, "Local (Metro Manila / Rizal)"),
        [Zone.NearLuzon]      = (180m, "Near Luzon"),
        [Zone.CentralLuzon]   = (190m, "Central / Northern Luzon"),
        [Zone.FarNorthLuzon]  = (220m, "Far North Luzon"),
        [Zone.Bicol]          = (210m, "Bicol Region"),
    };

    /// <summary>Default fee shown before a province is selected. Falls back to Local.</summary>
    public static decimal DefaultFee => ZoneInfo[Zone.Local].Fee;

    /// <summary>True when we deliver to this province.</summary>
    public static bool IsServiceable(string? province)
        => !string.IsNullOrWhiteSpace(province) && ProvinceZone.ContainsKey(province);

    /// <summary>Shipping fee for a province, or null if not serviceable.</summary>
    public static decimal? FeeFor(string? province)
        => IsServiceable(province) ? ZoneInfo[ProvinceZone[province!]].Fee : null;

    /// <summary>Human-readable zone label for a province, or null if not serviceable.</summary>
    public static string? LabelFor(string? province)
        => IsServiceable(province) ? ZoneInfo[ProvinceZone[province!]].Label : null;

    /// <summary>All provinces we can deliver to, sorted alphabetically.</summary>
    public static IEnumerable<string> ServiceableProvinces => ProvinceZone.Keys.OrderBy(k => k);
}
