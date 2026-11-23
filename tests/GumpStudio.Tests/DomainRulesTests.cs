using GumpStudio.Domain.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using GumpStudio.Domain.Rules;

namespace GumpStudio.Tests;

public class StockRulesTests
{
    [Fact]
    public void Saida_maior_que_o_saldo_e_recusada()
    {
        var ex = Assert.Throws<DomainException>(() => StockRules.EnsureSufficientBalance(7m, 8m));
        Assert.Contains("insuficiente", ex.Message);
    }

    [Fact]
    public void Saida_igual_ao_saldo_e_permitida() => StockRules.EnsureSufficientBalance(7m, 7m);

    [Theory]
    [InlineData(7, 10, true)]
    [InlineData(10, 10, false)]
    [InlineData(0, 0, false)]   // mínimo zero = sem alerta
    public void Alerta_de_estoque_minimo(decimal saldo, decimal minimo, bool esperado) =>
        Assert.Equal(esperado, StockRules.IsBelowMinimum(saldo, minimo));
}

public class StockMovementTests
{
    private static readonly Guid P = Guid.NewGuid(), S = Guid.NewGuid(), U = Guid.NewGuid(), Sess = Guid.NewGuid();

    [Fact]
    public void Entrada_soma_e_exige_usuario()
    {
        var m = StockMovement.Entry(P, S, 10, 4.5m, null, null, null, U, null);
        Assert.Equal(10, m.QuantityDelta);
        Assert.Throws<DomainException>(() => StockMovement.Entry(P, S, 10, 4.5m, null, null, null, Guid.Empty, null));
    }

    [Fact]
    public void Uso_exige_sessao_RN03()
    {
        Assert.Throws<DomainException>(() => StockMovement.Usage(P, S, null, 1, 4.5m, null, null, U, null));
    }

    [Fact]
    public void Uso_exige_valor_pago_RN04_e_aceita_venda_nula_RN05()
    {
        Assert.Throws<DomainException>(() => StockMovement.Usage(P, S, Sess, 1, null, null, null, U, null));
        var m = StockMovement.Usage(P, S, Sess, 2, 4.5m, null, null, U, null);
        Assert.Equal(-2, m.QuantityDelta);
        Assert.Null(m.UnitSalePrice);
    }

    [Fact]
    public void Quantidade_deve_ser_positiva()
    {
        Assert.Throws<DomainException>(() => StockMovement.Damaged(P, S, 0, "quebrou", null, U, null));
        Assert.Throws<DomainException>(() => StockMovement.InventoryAdjustment(P, S, -3, "contagem", null, U, null));
    }

    [Fact]
    public void Avaria_e_ajuste_exigem_motivo_e_sempre_reduzem()
    {
        Assert.Throws<DomainException>(() => StockMovement.Damaged(P, S, 1, " ", null, U, null));
        var adj = StockMovement.InventoryAdjustment(P, S, 3, "contagem", null, U, null);
        Assert.Equal(-3, adj.QuantityDelta);   // RN06: ajuste só reduz
    }

    [Fact]
    public void Correcao_inverte_a_movimentacao_original_e_nao_pode_ser_corrigida()
    {
        var entry = StockMovement.Entry(P, S, 10, 4.5m, null, null, null, U, null);
        var correction = StockMovement.CreateCorrection(entry, "lançado errado", U, null);

        Assert.Equal(-10, correction.QuantityDelta);
        Assert.Equal(StockMovementType.CorrectionExit, correction.Type);
        Assert.Equal(entry.Id, correction.ReversesMovementId);
        Assert.Throws<DomainException>(() => StockMovement.CreateCorrection(correction, "de novo", U, null));
    }
}

public class ArtistTests
{
    private static readonly DateOnly D = new(2026, 11, 1);

    [Fact]
    public void Percentual_calcula_a_parte_do_tatuador_e_do_estudio()
    {
        var c = ArtistCompensation.Create(CompensationType.Percentage, null, null, 60m, D);
        Assert.Equal(600m, c.ArtistShareOf(1000m));
        Assert.Equal(400m, c.StudioShareOf(1000m));
    }

    [Theory]
    [InlineData(CompensationType.MonthlyFixed)]
    [InlineData(CompensationType.Daily)]
    [InlineData(CompensationType.Percentage)]
    public void Cada_modelo_exige_o_seu_valor(CompensationType type) =>
        Assert.Throws<DomainException>(() => ArtistCompensation.Create(type, null, null, null, D));

    [Fact]
    public void Percentual_acima_de_100_e_invalido() =>
        Assert.Throws<DomainException>(() => ArtistCompensation.Create(CompensationType.Percentage, null, null, 101m, D));

    [Fact]
    public void Trocar_remuneracao_encerra_a_anterior_sem_perder_historico()
    {
        var artist = TattooArtist.Create(Guid.NewGuid(), "João", "joao@x.com", ArtistType.Fixed, D, null,
            ArtistCompensation.Create(CompensationType.Percentage, null, null, 50m, D));

        artist.ChangeCompensation(ArtistCompensation.Create(CompensationType.MonthlyFixed, 2500m, null, null, D.AddDays(30)));

        Assert.Equal(CompensationType.Percentage, artist.CompensationOn(D.AddDays(10))!.Type);
        Assert.Equal(CompensationType.MonthlyFixed, artist.CompensationOn(D.AddDays(31))!.Type);
        Assert.Equal(2, artist.Compensations.Count);
    }

    [Fact]
    public void Rotativo_exige_data_final() =>
        Assert.Throws<DomainException>(() => TattooArtist.Create(Guid.NewGuid(), "Maria", "m@x.com", ArtistType.Rotating, D, null,
            ArtistCompensation.Create(CompensationType.Daily, null, 150m, null, D)));

    [Fact]
    public void Permissao_por_estoque()
    {
        var artist = TattooArtist.Create(Guid.NewGuid(), "João", "joao@x.com", ArtistType.Fixed, D, null,
            ArtistCompensation.Create(CompensationType.Daily, null, 150m, null, D));
        var s1 = Guid.NewGuid();
        artist.SetStockAccess([s1]);
        Assert.True(artist.CanUseStock(s1));
        Assert.False(artist.CanUseStock(Guid.NewGuid()));
    }
}

public class UserAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rotativo_e_bloqueado_depois_da_data_final()
    {
        var u = User.Create("a@x.com", "A", UserRole.Artist, Now.AddDays(-5), Now.AddDays(2));
        u.EnsureCanAuthenticate(Now);   // dentro da janela
        Assert.Equal(AccessStatus.Expired, u.GetAccessStatus(Now.AddDays(2)));
        Assert.Throws<AccessDeniedException>(() => u.EnsureCanAuthenticate(Now.AddDays(3)));
    }

    [Fact]
    public void Acesso_ainda_nao_iniciado_e_bloqueado()
    {
        var u = User.Create("a@x.com", "A", UserRole.Artist, Now.AddDays(1), Now.AddDays(5));
        Assert.Equal(AccessStatus.NotStarted, u.GetAccessStatus(Now));
    }

    [Fact]
    public void Usuario_desativado_nao_entra()
    {
        var u = User.Create("a@x.com", "A", UserRole.Admin);
        u.Deactivate();
        Assert.Throws<AccessDeniedException>(() => u.EnsureCanAuthenticate(Now));
    }

    [Fact]
    public void Email_e_normalizado() => Assert.Equal("a@x.com", User.Create("  A@X.com ", "A", UserRole.Admin).Email);
}

public class SessionTests
{
    private static TattooSession New() =>
        TattooSession.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), 120, 500m, null, null, null);

    [Fact]
    public void Fluxo_normal_agendada_confirmada_em_andamento_concluida()
    {
        var s = New();
        s.Confirm(); s.Start(); s.Complete(DateTimeOffset.UtcNow);
        Assert.Equal(SessionStatus.Completed, s.Status);
        Assert.NotNull(s.CompletedAt);
    }

    [Fact]
    public void Nao_conclui_sem_iniciar() => Assert.Throws<DomainException>(() => New().Complete(DateTimeOffset.UtcNow));

    [Fact]
    public void Concluida_nao_pode_ser_cancelada()
    {
        var s = New(); s.Start(); s.Complete(DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => s.Cancel("x"));
    }

    [Fact]
    public void So_reagenda_enquanto_agendada_ou_confirmada()
    {
        var s = New(); s.Start();
        Assert.Throws<DomainException>(() => s.Reschedule(Guid.NewGuid(), DateTimeOffset.UtcNow, 60));
    }

    [Fact]
    public void Duracao_define_o_fim()
    {
        var s = New();
        Assert.Equal(TimeSpan.FromMinutes(120), s.EndsAt - s.StartsAt);
    }

    [Fact]
    public void Falha_do_google_nao_altera_a_sessao()
    {
        var s = New();
        s.MarkCalendarFailed("timeout");
        Assert.Equal(CalendarSyncStatus.Failed, s.CalendarSyncStatus);
        Assert.Equal(SessionStatus.Scheduled, s.Status);
        s.MarkCalendarSynced("evt1");
        Assert.Equal("evt1", s.GoogleCalendarEventId);
        Assert.Null(s.CalendarSyncError);
    }
}

public class FinancialTransactionTests
{
    private static readonly DateOnly D = new(2026, 11, 23);

    [Fact]
    public void Valor_deve_ser_positivo() =>
        Assert.Throws<DomainException>(() => FinancialTransaction.Create(TransactionType.Income, "x", 0, D, "Tatuagem", null, null, null, null));

    [Fact]
    public void Estorno_tem_valor_negativo_e_nao_pode_ser_estornado()
    {
        var t = FinancialTransaction.Create(TransactionType.Income, "Tatuagem", 500m, D, "Tatuagem", null, null, null, null);
        var r = FinancialTransaction.Reverse(t, D, null);
        Assert.Equal(-500m, r.Amount);
        Assert.Equal(t.Id, r.ReversesTransactionId);
        Assert.Throws<DomainException>(() => FinancialTransaction.Reverse(r, D, null));
    }
}
