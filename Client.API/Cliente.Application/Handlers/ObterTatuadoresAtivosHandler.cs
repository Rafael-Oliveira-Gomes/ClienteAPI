using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Tatuador.Application.Handlers;

public class ObterTatuadoresAtivosHandler(ITatuadorRepository tatuadorRepository)
    : IRequestHandler<TatuadoresAtivosQuery, Response<IEnumerable<TatuadorViewModel>>>
{
    public async Task<Response<IEnumerable<TatuadorViewModel>>> Handle(TatuadoresAtivosQuery request, CancellationToken cancellationToken)
    {
        var tatuadores = await tatuadorRepository.ConsultarTodosAtivos();
        var viewModels = tatuadores.Select(t => new TatuadorViewModel(t));
        return new Response<IEnumerable<TatuadorViewModel>>(viewModels);
    }
}
