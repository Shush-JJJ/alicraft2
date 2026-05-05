namespace Alicraft2.Data;

/// <summary>
/// Shipping-fee tiers based on the customer's province.
/// Origin: Karangalan Village, Brgy. San Isidro, Cainta, Rizal.
/// We only deliver to Luzon — provinces outside this list are blocked at
/// checkout. Fees are flat per zone (no per-km pricing) so customers see a
/// stable amount that's easy to reason about.
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

    private static readonly Dictionary<Zone, (decimal Fee, string Label)> ZoneInfo = new()
    {
        [Zone.Local]          = (60m,  "Local (Metro Manila / Rizal)"),
        [Zone.NearLuzon]      = (90m,  "Near Luzon"),
        [Zone.CentralLuzon]   = (130m, "Central / Northern Luzon"),
        [Zone.FarNorthLuzon]  = (180m, "Far North Luzon"),
        [Zone.Bicol]          = (180m, "Bicol Region"),
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
