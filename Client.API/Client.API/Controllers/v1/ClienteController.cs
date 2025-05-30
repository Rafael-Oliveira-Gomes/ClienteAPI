using System.Net;
using Client.Domain.Entities.Command;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Client.API.Controllers.v1;

/// <summary>
/// Controlador responsável por gerenciar operações relacionadas a clientes na API v1.
/// </summary>s
[ApiController]
[Route("api/v1/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="ClienteController"/>.
    /// </summary>
    /// <param name="mediator">Instância do MediatR para envio de comandos e consultas.</param>
    public ClienteController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cria um novo cliente.
    /// </summary>
    /// <param name="command">Comando contendo os dados do cliente a ser criado.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) se criado com sucesso ou 400 (Bad Request) em caso de erro.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateCliente([FromBody] IncluirClienteCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Obtém um cliente pelo ID.
    /// </summary>
    /// <param name="clienteId">ID do cliente a ser consultado.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) com os dados do cliente ou 404 (Not Found) se não encontrado.</returns>
    [HttpGet("{clienteId:int}")]
    public async Task<IActionResult> GetClienteById([FromRoute] int clienteId, CancellationToken cancellationToken)
    {
        var query = new ClienteQuery(clienteId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsSuccess && result.Data != null)
            return Ok(result);

        if (result.HttpStatusCode == HttpStatusCode.NotFound)
            return NotFound(result);

        return BadRequest(result);
    }
    /// <summary>
    /// Obtém todos os clientes cadastrados.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Retorna 200 (OK) com a lista de clientes ou 400 (Bad Request) em caso de erro.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllClientes(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TodosClientesQuery(), cancellationToken);

        if (result is IEnumerable<ClienteViewModel> clientes)
            return Ok(clientes);

        return BadRequest("Erro ao obter a lista de clientes.");
    }
}
