using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AtomicRegistry.Controllers;

[Route("/api/faults")]
public class FaultController : ControllerBase
{
    private readonly FaultSettingsProvider faultSettingsProvider;

    public FaultController(FaultSettingsProvider faultSettingsProvider)
    {
        this.faultSettingsProvider = faultSettingsProvider;
    }

    [HttpPost("push")]
    public void Push([FromBody] FaultSettingsDto newFaultSettings)
    {
        faultSettingsProvider.Push(newFaultSettings);
    }
}