using Client.Domain.Entities;
using Client.Domain.Entities.Command;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Repositories;
using Client.Domain.Shareds;
using MediatR;

namespace Cliente.Application.Handlers;

public class IncluirSessaoHandler(ISessaoRepository sessaoRepository, ITatuadorRepository tatuadorRepository, IClienteRepository clienteRepository) : IRequestHandler<IncluirSessaoCommand, Response<SessaoViewModel>>
{
    public async Task<Response<SessaoViewModel>> Handle(IncluirSessaoCommand request, CancellationToken cancellationToken)
    {
        var tatuador = await tatuadorRepository.ConsultarPorId(request.sessaoDto.TatuadorId);
        var cliente = await clienteRepository.ConsultarPorId(request.sessaoDto.ClienteId);
        var sessao = new Sessao
        {
            tatuador = tatuador!,
            cliente = cliente!,
            DataHora = request.sessaoDto.DataAgendamento,
            Duracao = request.sessaoDto.Duracao,
            Status = request.sessaoDto.Status,
            Descricao = request.sessaoDto.DescricaoTatuagem,
            Local = request.sessaoDto.LocalizacaoCorpo,
            Observacoes = request.sessaoDto.Observacoes,
            Cuidados = request.sessaoDto.Cuidados
        };
        await sessaoRepository.AddAsync(sessao);
        var sessaoViewModel = new SessaoViewModel(sessao);

        return new(sessaoViewModel);
    }
}
