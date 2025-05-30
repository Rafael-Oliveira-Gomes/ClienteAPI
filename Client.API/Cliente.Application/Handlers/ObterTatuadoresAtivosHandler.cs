using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using MediatR;

namespace Tatuador.Application.Handlers;

public class ObterTatuadoresAtivosHandler(ITatuadorRepository tatuadorRepository) : IRequestHandler<TatuadoresAtivosQuery, IEnumerable<TatuadorViewModel>>
{
    public async Task<IEnumerable<TatuadorViewModel>> Handle(TatuadoresAtivosQuery request, CancellationToken cancellationToken)
    {
        var tatuadores = await tatuadorRepository.ConsultarTodosAtivos();
        return tatuadores.Select(c => new TatuadorViewModel(c));
    }
}
