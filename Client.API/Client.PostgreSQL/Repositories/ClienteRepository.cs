using Client.Domain.Entities;
using Client.Domain.Repositories;
using Client.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Client.PostgreSQL.Repositories;

public class ClienteRepository : BaseRepository<Cliente>, IClienteRepository
{
    public ClienteRepository(ClientContext context) : base(context)
    {
    }
    public async Task<Cliente?> ConsultarPorId(int id)
    {
        return await _context.Clientes.FindAsync(id);
    }

    public async Task<Cliente?> ConsultarPorNome(string nome)
    {
        return await _context.Clientes
            .FirstOrDefaultAsync(c => c.Nome == nome);
    }
    public async Task<IEnumerable<Cliente>> ConsultarTodos()
    {
        return await _context.Clientes.ToListAsync();
    }

}