using Client.Domain.Entities.Command;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Client.API.Controllers.v1;

/// <summary>
/// Controlador responsável por gerenciar operações relacionadas a sessões na API v1.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class SessaoController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="SessaoController"/>.
    /// </summary>
    /// <param name="mediator">Instância do MediatR para envio de comandos e consultas.</param>
    public SessaoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cria uma nova sessão.
    /// </summary>
    /// <param name="command">Comando contendo os dados da sessão a ser criada.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) se criado com sucesso ou 400 (Bad Request) em caso de erro.</returns>
    [HttpPost]
    public async Task<IActionResult> IncluirSessao([FromBody] IncluirSessaoCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
