using Alicraft2.Data;
using Microsoft.AspNetCore.Mvc;

namespace Alicraft2.Controllers;

[Route("api/locations")]
public class LocationsController : Controller
{
    // We only deliver inside Luzon, so the dropdowns everywhere (register,
    // profile, checkout) are restricted to the serviceable list. Cities still
    // resolve via the full LocationData (it returns empty for unknown provinces).
    [HttpGet("provinces")]
    public IActionResult Provinces()
        => Json(ShippingZones.ServiceableProvinces);

    [HttpGet("cities")]
    public IActionResult Cities([FromQuery] string province)
        => Json(LocationData.CitiesFor(province ?? string.Empty));

    /// <summary>
    /// Returns the shipping fee + zone label for a province, or
    /// <c>{ serviceable: false }</c> if we don't deliver there. Used by the
    /// checkout form to live-update the order total when the user changes
    /// province.
    /// </summary>
    [HttpGet("shipping-fee")]
    public IActionResult ShippingFee([FromQuery] string? province)
    {
        if (!ShippingZones.IsServiceable(province))
            return Json(new { serviceable = false });
        return Json(new
        {
            serviceable = true,
            fee = ShippingZones.FeeFor(province),
            zone = ShippingZones.LabelFor(province)
        });
    }
}
