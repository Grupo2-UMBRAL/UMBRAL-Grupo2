using Microsoft.AspNetCore.Mvc;

namespace Umbral.ServiceDefaults;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
}
