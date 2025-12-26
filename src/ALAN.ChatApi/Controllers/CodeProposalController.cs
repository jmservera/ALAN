using ALAN.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.ChatApi.Controllers;

[ApiController]
[Route("api/proposals")]
public class CodeProposalController : ControllerBase
{
    private readonly ILogger<CodeProposalController> _logger;
    // TODO: Connect to actual CodeProposalService when running together
    
    public CodeProposalController(ILogger<CodeProposalController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet]
    public IActionResult GetProposals([FromQuery] ProposalStatus? status = null)
    {
        _logger.LogInformation("Getting proposals with status filter: {Status}", status);
        
        // TODO: Return actual proposals from CodeProposalService
        var proposals = new List<CodeProposal>
        {
            new CodeProposal
            {
                Id = "proposal-1",
                Title = "Improve error handling",
                Description = "Add try-catch blocks to critical sections",
                Status = ProposalStatus.Pending,
                Created = DateTime.UtcNow.AddHours(-2)
            }
        };
        
        if (status.HasValue)
        {
            proposals = proposals.Where(p => p.Status == status.Value).ToList();
        }
        
        return Ok(proposals);
    }
    
    [HttpGet("{id}")]
    public IActionResult GetProposal(string id)
    {
        _logger.LogInformation("Getting proposal: {Id}", id);
        
        // TODO: Get actual proposal from CodeProposalService
        var proposal = new CodeProposal
        {
            Id = id,
            Title = "Sample Proposal",
            Description = "Sample code change",
            Status = ProposalStatus.Pending
        };
        
        return Ok(proposal);
    }
    
    [HttpPost("{id}/approve")]
    public IActionResult ApproveProposal(string id, [FromBody] ApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.ApprovedBy))
        {
            return BadRequest("ApprovedBy is required");
        }

        _logger.LogInformation("Approving proposal {Id} by {ApprovedBy}", id, request.ApprovedBy);
        
        // TODO: Forward to CodeProposalService
        
        return Ok(new
        {
            proposalId = id,
            status = "Approved",
            approvedBy = request.ApprovedBy,
            timestamp = DateTime.UtcNow
        });
    }
    
    [HttpPost("{id}/reject")]
    public IActionResult RejectProposal(string id, [FromBody] RejectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest("Reason is required");
        }

        _logger.LogInformation("Rejecting proposal {Id}: {Reason}", id, request.Reason);
        
        // TODO: Forward to CodeProposalService
        
        return Ok(new
        {
            proposalId = id,
            status = "Rejected",
            reason = request.Reason,
            timestamp = DateTime.UtcNow
        });
    }
}

public record ApprovalRequest(string ApprovedBy);
public record RejectionRequest(string Reason);
