using Client.Domain.Entities.Command;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Cliente.Application.Handlers;

public class IncluirTatuadorHandler : IRequestHandler<IncluirTatuadorCommand, Response<TatuadorViewModel>>
{
    private readonly ITatuadorRepository _tatuadorRepository;

    public IncluirTatuadorHandler(ITatuadorRepository tatuadorRepository)
    {
        _tatuadorRepository = tatuadorRepository;
    }
    public async Task<Response<TatuadorViewModel>> Handle(IncluirTatuadorCommand request, CancellationToken cancellationToken)
    {
        var tatuador = new Client.Domain.Entities.Tatuador
        {
            Nome = request.Tatuador.Nome,
            NomeArtistico = request.Tatuador.NomeArtistico,
            DataNascimento = request.Tatuador.DataNascimento,
            Email = request.Tatuador.Email,
            Telefone = request.Tatuador.Telefone,
            AnoExperiencia = request.Tatuador.AnoExperiencia,
            Especialidade = request.Tatuador.Especialidade,
            Portifolio = request.Tatuador.Portifolio,
            Ativo = request.Tatuador.Ativo,
            Observacao = request.Tatuador.Observacao,
        };

        await _tatuadorRepository.AddAsync(tatuador);

        var tatuadorViewModel = new TatuadorViewModel(tatuador);

        return new(tatuadorViewModel);
    }
}
