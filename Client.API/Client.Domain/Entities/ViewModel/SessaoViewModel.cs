namespace Client.Domain.Entities.ViewModel;

public record class SessaoViewModel
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

    public SessaoViewModel(Sessao sessao)
    {
        this.tatuador = sessao.tatuador ?? throw new ArgumentNullException(nameof(sessao.tatuador));
        this.cliente = sessao.cliente ?? throw new ArgumentNullException(nameof(sessao.cliente));
        DataHora = sessao.DataHora;
        Duracao = sessao.Duracao;
        Status = sessao.Status;
        Descricao = sessao.Descricao;
        Local = sessao.Local;
        Observacoes = sessao.Observacoes;
        Cuidados = sessao.Cuidados;
    }
}
