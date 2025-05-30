using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Cliente.Application.Handlers;

public class ObterTodosClientesHandler : IRequestHandler<TodosClientes, Response<IEnumerable<ClienteViewModel>>>
{
    private readonly IClienteRepository _clienteRepository;

    public ObterTodosClientesHandler(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
    }

    public async Task<Response<IEnumerable<ClienteViewModel>>> Handle(TodosClientes request, CancellationToken cancellationToken)
    {
        var clientes = await _clienteRepository.ConsultarTodos();
        var viewModels = clientes.Select(c => new ClienteViewModel(c));
        return new Response<IEnumerable<ClienteViewModel>>(viewModels);
    }
}
