namespace Client.Domain.Entities;

public class Sessao
{
    public int Id { get; init; }
    public Tatuador tatuador { get; init; }
    public Cliente cliente { get; init; }
    public DateTime DataHora { get; init; }
    public float Duracao { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public string Local { get; init; } = string.Empty;
    public string Observacoes { get; init; } = string.Empty;
    public string Cuidados { get; init; } = string.Empty;

    public Sessao() { }

    public Sessao(Tatuador tatuador, Cliente cliente, DateTime dataHora, float duracao, string status, string descricao, string local, string observacoes, string cuidados)
    {
        this.tatuador = tatuador;
        this.cliente = cliente;
        DataHora = dataHora;
        Duracao = duracao;
        Status = status;
        Descricao = descricao;
        Local = local;
        Observacoes = observacoes;
        Cuidados = cuidados;
    }
}
