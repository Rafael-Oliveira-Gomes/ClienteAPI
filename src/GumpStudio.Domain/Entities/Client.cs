using GumpStudio.Domain.Common;

namespace GumpStudio.Domain.Entities;

public sealed class Client : AuditableEntity
{
    private Client() { }

    public string Name { get; private set; } = "";
    public string? Cpf { get; private set; }
    public string? Phone { get; private set; }
    public string? WhatsApp { get; private set; }
    public string? Email { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }

    public static Client Create(string name, string? cpf, string? phone, string? whatsApp, string? email,
        DateOnly? birthDate, string? address, string? notes)
    {
        var c = new Client();
        c.Update(name, cpf, phone, whatsApp, email, birthDate, address, notes);
        return c;
    }

    public void Update(string name, string? cpf, string? phone, string? whatsApp, string? email,
        DateOnly? birthDate, string? address, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome é obrigatório.");
        Name = name.Trim(); Cpf = cpf; Phone = phone; WhatsApp = whatsApp;
        Email = email?.Trim().ToLowerInvariant(); BirthDate = birthDate; Address = address; Notes = notes;
    }
}
