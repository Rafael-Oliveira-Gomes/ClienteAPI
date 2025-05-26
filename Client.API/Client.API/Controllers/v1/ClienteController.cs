using System.Threading;
using Client.Domain.Entities.Command;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Client.API.Controllers.v1;
[ApiController]
[Route("api/v1/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClienteController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCliente([FromBody] IncluirClienteCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
