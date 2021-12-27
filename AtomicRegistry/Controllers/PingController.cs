using Microsoft.AspNetCore.Mvc;

namespace AtomicRegistry.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        return "Ok";
    }
}