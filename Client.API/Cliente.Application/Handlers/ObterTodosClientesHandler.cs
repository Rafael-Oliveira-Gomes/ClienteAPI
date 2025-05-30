using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using MediatR;

namespace Cliente.Application.Handlers;

public class ObterTodosClientesHandler(IClienteRepository clienteRepository) : IRequestHandler<TodosClientesQuery, IEnumerable<ClienteViewModel>>
{
    public async Task<IEnumerable<ClienteViewModel>> Handle(TodosClientesQuery request, CancellationToken cancellationToken)
    {
        var clientes = await clienteRepository.ConsultarTodos();
        return clientes.Select(c => new ClienteViewModel(c));
    }
}
