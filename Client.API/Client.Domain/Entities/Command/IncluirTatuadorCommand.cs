using Client.Domain.DTOs;
using Client.Domain.Entities.ViewModel;
using Client.Domain.Shareds;
using MediatR;

namespace Client.Domain.Entities.Command;

public record class IncluirTatuadorCommand(TatuadorDto Tatuador) : IRequest<Response<TatuadorViewModel>>;
