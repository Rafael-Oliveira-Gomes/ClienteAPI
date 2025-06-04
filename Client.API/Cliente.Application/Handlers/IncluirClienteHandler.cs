using Client.Domain.Entities.Command;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;
using ClienteEntity = Client.Domain.Entities.Cliente; 

namespace Cliente.Application.Handlers;

public class IncluirClienteHandler(IClienteRepository clienteRepository) : IRequestHandler<IncluirClienteCommand, Response<ClienteViewModel>>
{
    public async Task<Response<ClienteViewModel>> Handle(IncluirClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = new ClienteEntity
        {
            Nome = request.Cliente.Nome,
            Email = request.Cliente.Email,
            Telefone = request.Cliente.Telefone,
            DataNascimento = request.Cliente.DataNascimento,
            DataCadastro = DateTime.UtcNow,
            Alergia = request.Cliente.Alergia,
            Observacao = request.Cliente.Observacao,
            Sexo = request.Cliente.Sexo,
            NomeSocial = request.Cliente.NomeSocial
        };

         await clienteRepository.AddAsync(cliente);

        var clienteViewModel = new ClienteViewModel(cliente);

        return new (clienteViewModel);

    }
}
