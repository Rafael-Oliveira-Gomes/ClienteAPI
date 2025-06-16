using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using MediatR;

namespace Cliente.Application.Handlers.Sessao;

public class ObterTodasSessaoHandler(ISessaoRepository sessaoRepository) : IRequestHandler<TodasSessaoQuery, IEnumerable<SessaoViewModel>>
{
    public async Task<IEnumerable<SessaoViewModel>> Handle(TodasSessaoQuery request, CancellationToken cancellationToken)
    {
        var sessoes = await sessaoRepository.ConsultarTodos();
        return sessoes.Select(s => new SessaoViewModel(s));
    }
}