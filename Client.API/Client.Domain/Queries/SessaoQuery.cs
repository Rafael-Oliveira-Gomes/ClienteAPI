using Client.Domain.Entities.ViewModel;
using MediatR;

namespace Client.Domain.Queries;

public record class TodasSessaoQuery() : IRequest<IEnumerable<SessaoViewModel>>;
