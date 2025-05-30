﻿namespace Client.Domain.DTOs;

public class ClienteDto
{
    public string Nome { get; set; } = string.Empty;
    public string NomeSocial { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public DateOnly DataNascimento { get; set; }
    public bool Alergia { get; set; }
    public string Observacao { get; set; } = string.Empty;
    public string Sexo { get; set; } = string.Empty;
}
