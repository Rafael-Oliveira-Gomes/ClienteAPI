using Client.Domain.Entities.ViewModel;
using Client.Domain.Queries;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Tatuador.Application.Handlers;

public class ObterTatuadorHandler(ITatuadorRepository tatuadorRepository) : IRequestHandler<TatuadorQuery, Response<TatuadorViewModel>>
{
    public async Task<Response<TatuadorViewModel>> Handle(TatuadorQuery request, CancellationToken cancellationToken)
    {
        var tatuador = await tatuadorRepository.ConsultarPorId(request.tatuadorId);

        if (tatuador == null)
        {
            return new Response<TatuadorViewModel>("Tatuador não encontrado.");
        }
        var TatuadorViewModel = new TatuadorViewModel(tatuador);
        return new Response<TatuadorViewModel>(TatuadorViewModel);
    }
}