using Client.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Client.PostgreSQL.Context;

public class ClientContext : DbContext
{
    public ClientContext(DbContextOptions<ClientContext> options) : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public DbSet<Cliente> Cliente { get; set; }
}
