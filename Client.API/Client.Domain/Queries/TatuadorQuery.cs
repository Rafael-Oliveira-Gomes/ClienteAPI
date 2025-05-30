using Client.Domain.Entities.ViewModel;
using Client.Domain.Shareds;
using MediatR;

namespace Client.Domain.Queries;

public record class TatuadorQuery(int tatuadorId) : IRequest<Response<TatuadorViewModel>>;

public record class TatuadoresAtivosQuery() : IRequest<Response<IEnumerable<TatuadorViewModel>>>;