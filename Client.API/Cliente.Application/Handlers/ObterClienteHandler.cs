using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Cliente.Application.Handlers;

public class ObterClienteHandler(IClienteRepository clienteRepository) : IRequestHandler<ClienteQuery, Response<ClienteViewModel>>
{
    public async Task<Response<ClienteViewModel>> Handle(ClienteQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.ConsultarPorId(request.clienteId);

        if (cliente == null)
        {
            return new Response<ClienteViewModel>("Cliente não encontrado.");
        }
        var clienteViewModel = new ClienteViewModel(cliente);
        return new Response<ClienteViewModel>(clienteViewModel);
    }
}
