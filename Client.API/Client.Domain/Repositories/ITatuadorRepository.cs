using Client.Domain.Entities;

namespace Client.Domain.Repositories;

public interface ITatuadorRepository : IBaseRepository<Tatuador>
{
    Task<Tatuador?> ConsultarPorId(int id);
    Task<Tatuador?> ConsultarPorNome(string nome);
    Task<IEnumerable<Tatuador>> ConsultarTodosAtivos();
}
