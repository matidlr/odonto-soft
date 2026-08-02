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
    public DbSet<TipoTratamiento> TiposTratamiento => Set<TipoTratamiento>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<EventoOdontograma> EventosOdontograma => Set<EventoOdontograma>();
    public DbSet<ArchivoOdontograma> ArchivosOdontograma => Set<ArchivoOdontograma>();
    public DbSet<TokenResetPassword> TokensResetPassword => Set<TokenResetPassword>();
    public DbSet<FichaMedica> FichasMedicas => Set<FichaMedica>();
    public DbSet<NotaEvolucion> NotasEvolucion => Set<NotaEvolucion>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<ArchivoPaciente> ArchivosPaciente => Set<ArchivoPaciente>();

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

            b.HasQueryFilter(p => _tenantContext.EsSuperAdmin || p.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Disponibilidad>(b =>
        {
            b.HasOne(d => d.Tenant).WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(d => d.Odontologo).WithMany().HasForeignKey(d => d.OdontologoId).OnDelete(DeleteBehavior.Cascade);

            b.HasQueryFilter(d => _tenantContext.EsSuperAdmin || d.TenantId == _tenantContext.TenantId);
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

            b.HasQueryFilter(a => _tenantContext.EsSuperAdmin || a.TenantId == _tenantContext.TenantId);
        });
    }
}
