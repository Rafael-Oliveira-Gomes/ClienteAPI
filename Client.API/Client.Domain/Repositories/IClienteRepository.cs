using Client.Domain.Entities;

namespace Client.Domain.Repositories;

public interface IClienteRepository : IBaseRepository<Cliente>
{
    Task<Cliente?> ConsultarPorId(int id);
    Task<Cliente?> ConsultarPorNome(string nome);
}
