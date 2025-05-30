using Client.Domain.Entities.ViewModel;
using Client.Domain.Shareds;
using MediatR;

namespace Client.Domain.Queries;

public record class ClienteQuery(int clienteId) : IRequest<Response<ClienteViewModel>>;

public record class TodosClientes() : IRequest<Response<IEnumerable<ClienteViewModel>>>;