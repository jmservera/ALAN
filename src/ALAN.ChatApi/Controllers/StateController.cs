using ALAN.ChatApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.ChatApi.Controllers;

[ApiController]
[Route("api")]
public class StateController : ControllerBase
{
    private readonly AgentStateService _stateService;
    
    public StateController(AgentStateService stateService)
    {
        _stateService = stateService;
    }
    
    [HttpGet("state")]
    public IActionResult GetState()
    {
        var state = _stateService.GetCurrentState();
        return Ok(state);
    }
}
