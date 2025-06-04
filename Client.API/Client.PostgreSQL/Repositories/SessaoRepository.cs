using Client.Domain.Entities;
using Client.Domain.Repositories;
using Client.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Client.PostgreSQL.Repositories;

public class SessaoRepository : BaseRepository<Sessao>, ISessaoRepository
{
    public SessaoRepository(ClientContext context) : base(context)
    {
    }

    public async Task<Sessao?> ConsultarPorId(int id)
    {
        return await _dbSet
            .Include(s => s.tatuador)
            .Include(s => s.cliente)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Sessao>> ConsultarSessaoPorDia(DateOnly diaSessao)
    {
        var inicio = diaSessao.ToDateTime(TimeOnly.MinValue);
        var fim = diaSessao.ToDateTime(TimeOnly.MaxValue);

        return await _dbSet
            .Include(s => s.tatuador)
            .Include(s => s.cliente)
            .Where(s => s.DataHora >= inicio && s.DataHora <= fim)
            .ToListAsync();
    }
}