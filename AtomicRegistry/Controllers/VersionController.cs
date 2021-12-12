﻿using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace AtomicRegistry.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class VersionController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return ((AssemblyInformationalVersionAttribute) Assembly.GetExecutingAssembly().GetCustomAttribute(
                typeof(AssemblyInformationalVersionAttribute))!).InformationalVersion;
        }
    }
}