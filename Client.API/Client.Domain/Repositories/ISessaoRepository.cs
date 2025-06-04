using Client.Domain.Entities;

namespace Client.Domain.Repositories;

public interface ISessaoRepository : IBaseRepository<Sessao>
{
    Task<Sessao?> ConsultarPorId(int id);
    Task<IEnumerable<Sessao>> ConsultarSessaoPorDia(DateOnly diaSessao);
}
