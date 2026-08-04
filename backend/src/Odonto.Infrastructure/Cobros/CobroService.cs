using Microsoft.EntityFrameworkCore;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Infrastructure.Cobros;

public class CobroService : ICobroService
{
    private readonly AppDbContext _db;

    public CobroService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SaldoPaciente> CalcularSaldoAsync(Guid pacienteId, CancellationToken ct = default)
    {
        var totalAprobado = await _db.Presupuestos
            .Where(p => p.PacienteId == pacienteId && p.Estado == EstadoPresupuesto.Aprobado)
            .SelectMany(p => p.Items)
            .SumAsync(i => (decimal?)(i.Cantidad * i.PrecioUnitario), ct) ?? 0m;

        var totalCobrado = await _db.Cobros
            .Where(c => c.PacienteId == pacienteId)
            .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

        return new SaldoPaciente(totalAprobado, totalCobrado, totalAprobado - totalCobrado);
    }
}
