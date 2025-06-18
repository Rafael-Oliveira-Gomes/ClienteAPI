using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using MediatR;

namespace Cliente.Application.Handlers.Sessao;

/// <summary>
/// Handler para obter uma sessão específica pelo ID.
/// </summary>
public class ObterSessaoHandler(ISessaoRepository sessaoRepository) : IRequestHandler<SessaoQuery, SessaoViewModel?>
{
    public async Task<SessaoViewModel?> Handle(SessaoQuery request, CancellationToken cancellationToken)
    {
        var sessao = await sessaoRepository.ConsultarPorId(request.idSessao);
        if (sessao == null)
            return null;

        return new SessaoViewModel(sessao);
    }
}