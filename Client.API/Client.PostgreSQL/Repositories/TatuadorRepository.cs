using Client.Domain.Entities;
using Client.Domain.Repositories;
using Client.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Client.PostgreSQL.Repositories;

public class TatuadorRepository : BaseRepository<Tatuador>, ITatuadorRepository
{
    public TatuadorRepository(ClientContext context) : base(context)
    {
    }

    public async Task<Tatuador?> ConsultarPorId(int id)
    {
        return await _context.Tatuadores.FindAsync(id);
    }

    public async Task<Tatuador?> ConsultarPorNome(string nome)
    {
        return await _context.Tatuadores
            .FirstOrDefaultAsync(c => c.Nome == nome);
    }
    public async Task<IEnumerable<Tatuador>> ConsultarTodosAtivos()
    {
        return await _context.Tatuadores
            .Where(t => t.Ativo)
            .ToListAsync();
    }
}
