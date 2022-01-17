using AtomicRegister.Configuration;
using AtomicRegister.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AtomicRegister.Controllers;

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