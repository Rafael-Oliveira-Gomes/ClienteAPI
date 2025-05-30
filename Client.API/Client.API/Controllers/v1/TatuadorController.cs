using System.Net;
using Client.Domain.Entities.Command;
using Client.Domain.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Client.API.Controllers.v1;

/// <summary>
/// Controlador responsável por gerenciar operações relacionadas a tatuadores na API v1.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class TatuadorController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="TatuadorController"/>.
    /// </summary>
    /// <param name="mediator">Instância do MediatR para envio de comandos e consultas.</param>
    public TatuadorController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cria um novo tatuador.
    /// </summary>
    /// <param name="command">Comando contendo os dados do tatuador a ser criado.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) se criado com sucesso ou 400 (Bad Request) em caso de erro.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateTatuador([FromBody] IncluirTatuadorCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Obtém um tatuador pelo ID.
    /// </summary>
    /// <param name="tatuadorId">ID do tatuador a ser consultado.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) com os dados do tatuador ou 404 (Not Found) se não encontrado.</returns>
    [HttpGet("{tatuadorId:int}")]
    public async Task<IActionResult> GetTatuadorById([FromRoute] int tatuadorId, CancellationToken cancellationToken)
    {
        var query = new TatuadorQuery(tatuadorId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsSuccess && result.Data != null)
            return Ok(result);

        if (result.HttpStatusCode == HttpStatusCode.NotFound)
            return NotFound(result);

        return BadRequest(result);
    }
    /// <summary>
    /// Lista todos os tatuadores ativos.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) com a lista de tatuadores ativos.</returns>
    [HttpGet("ativos")]
    public async Task<IActionResult> GetTatuadoresAtivos(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TatuadoresAtivosQuery(), cancellationToken);

        if (result != null && result.Any())
            return Ok(result);

        return NotFound("Nenhum tatuador ativo encontrado.");
    }
}
