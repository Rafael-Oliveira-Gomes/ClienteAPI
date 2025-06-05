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
        var tatuador = await tatuadorRepository.ConsultarPorId(request.Sessao.TatuadorId);
        var cliente = await clienteRepository.ConsultarPorId(request.Sessao.ClienteId);
        var sessao = new Sessao
        {
            tatuador = tatuador!,
            cliente = cliente!,
            DataHora = request.Sessao.DataAgendamento,
            Duracao = request.Sessao.Duracao,
            Status = request.Sessao.Status,
            Descricao = request.Sessao.DescricaoTatuagem,
            Local = request.Sessao.LocalizacaoCorpo,
            Observacoes = request.Sessao.Observacoes,
            Cuidados = request.Sessao.Cuidados
        };
        await sessaoRepository.AddAsync(sessao);
        var sessaoViewModel = new SessaoViewModel(sessao);

        return new(sessaoViewModel);
    }
}
