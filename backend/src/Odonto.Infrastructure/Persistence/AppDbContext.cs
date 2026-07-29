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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasIndex(t => t.Slug).IsUnique();
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
            b.HasOne(o => o.Usuario).WithMany().HasForeignKey(o => o.UsuarioId);

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
    }
}
