using Alicraft2.Data;
using Microsoft.AspNetCore.Mvc;

namespace Alicraft2.Controllers;

[Route("api/locations")]
public class LocationsController : Controller
{
    [HttpGet("provinces")]
    public IActionResult Provinces()
        => Json(LocationData.Provinces);

    [HttpGet("cities")]
    public IActionResult Cities([FromQuery] string province)
        => Json(LocationData.CitiesFor(province ?? string.Empty));
}
