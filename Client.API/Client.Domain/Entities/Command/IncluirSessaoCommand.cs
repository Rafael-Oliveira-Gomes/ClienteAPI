using Client.Domain.DTOs;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Shareds;
using MediatR;

namespace Client.Domain.Entities.Command;

public record class IncluirSessaoCommand(SessaoDto Sessao) : IRequest<Response<SessaoViewModel>>;
