using Microsoft.EntityFrameworkCore;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Entities;

namespace Odonto.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Odontologo> Odontologos => Set<Odontologo>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Disponibilidad> Disponibilidades => Set<Disponibilidad>();
    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<TipoTratamiento> TiposTratamiento => Set<TipoTratamiento>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<EventoOdontograma> EventosOdontograma => Set<EventoOdontograma>();
    public DbSet<ArchivoOdontograma> ArchivosOdontograma => Set<ArchivoOdontograma>();
    public DbSet<TokenResetPassword> TokensResetPassword => Set<TokenResetPassword>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FichaMedica> FichasMedicas => Set<FichaMedica>();
    public DbSet<NotaEvolucion> NotasEvolucion => Set<NotaEvolucion>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<ArchivoPaciente> ArchivosPaciente => Set<ArchivoPaciente>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();
    public DbSet<ItemPresupuesto> ItemsPresupuesto => Set<ItemPresupuesto>();
    public DbSet<Cobro> Cobros => Set<Cobro>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<Consentimiento> Consentimientos => Set<Consentimiento>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasIndex(t => t.Slug).IsUnique();

            b.HasOne(t => t.Plan).WithMany().HasForeignKey(t => t.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Plan>(b =>
        {
            b.Property(p => p.PrecioMensual).HasColumnType("decimal(10,2)");
            // Sin HasQueryFilter: es un catálogo compartido por toda la
            // plataforma, no un dato de negocio de un tenant en particular.
        });

        modelBuilder.Entity<Usuario>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();

            b.HasOne(u => u.Tenant)
                .WithMany(t => t.Usuarios)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // El SuperAdmin (EsSuperAdmin = true) no queda filtrado; el resto
            // de los usuarios solo ven filas de su propio tenant.
            b.HasQueryFilter(u => _tenantContext.EsSuperAdmin || u.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Odontologo>(b =>
        {
            b.HasOne(o => o.Tenant).WithMany().HasForeignKey(o => o.TenantId);
            b.HasOne(o => o.Usuario).WithMany().HasForeignKey(o => o.UsuarioId).OnDelete(DeleteBehavior.SetNull);

            b.Property(o => o.Nombre).IsRequired().HasDefaultValue("");

            b.HasQueryFilter(o => _tenantContext.EsSuperAdmin || o.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Paciente>(b =>
        {
            b.HasOne(p => p.Tenant)
                .WithMany(t => t.Pacientes)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.OdontologoPrincipal)
                .WithMany()
                .HasForeignKey(p => p.OdontologoPrincipalId)
                .OnDelete(DeleteBehavior.SetNull);

            // HasDefaultValue(true) para que los pacientes ya cargados (que
            // no tenían esta columna) queden Activos al agregarla, en vez de
            // desaparecer de los listados de golpe.
            b.Property(p => p.Activo).HasDefaultValue(true);

            b.HasQueryFilter(p => _tenantContext.EsSuperAdmin || p.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Sede>(b =>
        {
            b.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(s => s.Odontologo).WithMany().HasForeignKey(s => s.OdontologoId).OnDelete(DeleteBehavior.Cascade);

            b.HasQueryFilter(s => _tenantContext.EsSuperAdmin || s.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Disponibilidad>(b =>
        {
            b.HasOne(d => d.Tenant).WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(d => d.Odontologo).WithMany().HasForeignKey(d => d.OdontologoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(d => d.Sede).WithMany().HasForeignKey(d => d.SedeId).OnDelete(DeleteBehavior.Cascade);

            b.Property(d => d.IsDeleted).HasDefaultValue(false);

            b.HasQueryFilter(d => (_tenantContext.EsSuperAdmin || d.TenantId == _tenantContext.TenantId) && !d.IsDeleted);
        });

        modelBuilder.Entity<TipoTratamiento>(b =>
        {
            b.Property(t => t.PrecioBase).HasColumnType("decimal(10,2)");
            b.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);

            b.HasQueryFilter(t => _tenantContext.EsSuperAdmin || t.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Turno>(b =>
        {
            b.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.Odontologo).WithMany().HasForeignKey(t => t.OdontologoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.Paciente).WithMany().HasForeignKey(t => t.PacienteId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.TipoTratamiento).WithMany().HasForeignKey(t => t.TipoTratamientoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(t => t.Sede).WithMany().HasForeignKey(t => t.SedeId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(t => new { t.OdontologoId, t.FechaHora });

            b.HasQueryFilter(t => _tenantContext.EsSuperAdmin || t.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Notificacion>(b =>
        {
            b.HasOne(n => n.Turno).WithMany().HasForeignKey(n => n.TurnoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(n => new { n.Enviado, n.FechaEnvioProgramada });
        });

        modelBuilder.Entity<EventoOdontograma>(b =>
        {
            b.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Paciente).WithMany().HasForeignKey(e => e.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Odontologo).WithMany().HasForeignKey(e => e.OdontologoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(e => e.Turno).WithMany().HasForeignKey(e => e.TurnoId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(e => new { e.PacienteId, e.NumeroFdi, e.Fecha });

            b.HasQueryFilter(e => _tenantContext.EsSuperAdmin || e.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<ArchivoOdontograma>(b =>
        {
            b.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.EventoOdontograma).WithMany().HasForeignKey(a => a.EventoOdontogramaId).OnDelete(DeleteBehavior.Cascade);

            b.HasQueryFilter(a => _tenantContext.EsSuperAdmin || a.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<TokenResetPassword>(b =>
        {
            b.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(t => t.Token).IsUnique();
            // Sin HasQueryFilter a propósito: es una tabla de sistema, no de
            // negocio por tenant, y se consulta en endpoints anónimos.
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => r.TokenHash).IsUnique();
            b.HasIndex(r => r.UsuarioId);
            // Sin HasQueryFilter a propósito, mismo motivo que TokenResetPassword.
        });

        modelBuilder.Entity<FichaMedica>(b =>
        {
            b.HasOne(f => f.Tenant).WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(f => f.Paciente).WithMany().HasForeignKey(f => f.PacienteId).OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(f => f.PacienteId).IsUnique();

            b.HasQueryFilter(f => _tenantContext.EsSuperAdmin || f.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<NotaEvolucion>(b =>
        {
            b.HasOne(n => n.Tenant).WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(n => n.Paciente).WithMany().HasForeignKey(n => n.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(n => n.Odontologo).WithMany().HasForeignKey(n => n.OdontologoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(n => n.Turno).WithMany().HasForeignKey(n => n.TurnoId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(n => new { n.PacienteId, n.Fecha });

            b.HasQueryFilter(n => _tenantContext.EsSuperAdmin || n.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<ArchivoPaciente>(b =>
        {
            b.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.Paciente).WithMany().HasForeignKey(a => a.PacienteId).OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(a => new { a.PacienteId, a.Categoria });

            b.Property(a => a.IsDeleted).HasDefaultValue(false);

            b.HasQueryFilter(a => (_tenantContext.EsSuperAdmin || a.TenantId == _tenantContext.TenantId) && !a.IsDeleted);
        });

        modelBuilder.Entity<Presupuesto>(b =>
        {
            b.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(p => p.Paciente).WithMany().HasForeignKey(p => p.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(p => p.Odontologo).WithMany().HasForeignKey(p => p.OdontologoId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(p => new { p.PacienteId, p.FechaCreacion });

            b.Property(p => p.IsDeleted).HasDefaultValue(false);

            b.HasQueryFilter(p => (_tenantContext.EsSuperAdmin || p.TenantId == _tenantContext.TenantId) && !p.IsDeleted);
        });

        modelBuilder.Entity<ItemPresupuesto>(b =>
        {
            b.Property(i => i.PrecioUnitario).HasColumnType("decimal(10,2)");

            b.HasOne(i => i.Presupuesto).WithMany(p => p.Items).HasForeignKey(i => i.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(i => i.TipoTratamiento).WithMany().HasForeignKey(i => i.TipoTratamientoId).OnDelete(DeleteBehavior.SetNull);

            // Sin HasQueryFilter propio: siempre se accede a través del
            // Presupuesto dueño (que sí filtra por tenant), no directo.
        });

        modelBuilder.Entity<Cobro>(b =>
        {
            b.Property(c => c.Monto).HasColumnType("decimal(10,2)");

            b.HasOne(c => c.Tenant).WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(c => c.Paciente).WithMany().HasForeignKey(c => c.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(c => c.Presupuesto).WithMany().HasForeignKey(c => c.PresupuestoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(c => c.Odontologo).WithMany().HasForeignKey(c => c.OdontologoId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(c => new { c.PacienteId, c.Fecha });

            b.Property(c => c.IsDeleted).HasDefaultValue(false);

            b.HasQueryFilter(c => (_tenantContext.EsSuperAdmin || c.TenantId == _tenantContext.TenantId) && !c.IsDeleted);
        });

        modelBuilder.Entity<Insumo>(b =>
        {
            b.Property(i => i.StockActual).HasColumnType("decimal(10,2)");
            b.Property(i => i.StockMinimo).HasColumnType("decimal(10,2)");

            b.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

            b.HasQueryFilter(i => _tenantContext.EsSuperAdmin || i.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<MovimientoStock>(b =>
        {
            b.Property(m => m.Cantidad).HasColumnType("decimal(10,2)");

            b.HasOne(m => m.Tenant).WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(m => m.Insumo).WithMany().HasForeignKey(m => m.InsumoId).OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(m => new { m.InsumoId, m.Fecha });

            b.HasQueryFilter(m => _tenantContext.EsSuperAdmin || m.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Consentimiento>(b =>
        {
            b.HasOne(c => c.Tenant).WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(c => c.Paciente).WithMany().HasForeignKey(c => c.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(c => c.Odontologo).WithMany().HasForeignKey(c => c.OdontologoId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(c => new { c.PacienteId, c.FechaCreacion });

            b.Property(c => c.IsDeleted).HasDefaultValue(false);

            b.HasQueryFilter(c => (_tenantContext.EsSuperAdmin || c.TenantId == _tenantContext.TenantId) && !c.IsDeleted);
        });

        modelBuilder.Entity<RegistroAuditoria>(b =>
        {
            b.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.Paciente).WithMany().HasForeignKey(a => a.PacienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(a => new { a.PacienteId, a.Fecha });

            b.HasQueryFilter(a => _tenantContext.EsSuperAdmin || a.TenantId == _tenantContext.TenantId);
        });
    }
}
