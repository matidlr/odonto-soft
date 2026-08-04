using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odonto.Application.Common.Interfaces;
using Odonto.Infrastructure.Archivos;
using Odonto.Infrastructure.Notificaciones;
using Odonto.Infrastructure.Payments;
using Odonto.Infrastructure.Persistence;
using Odonto.Infrastructure.Seguridad;

namespace Odonto.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();

        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddHttpClient<IEmailSender, BrevoEmailSender>();
        services.AddHostedService<RecordatorioBackgroundService>();

        services.AddHttpClient<MercadoPagoService>();

        services.AddSingleton<IArchivoCifrado, ArchivoCifradoService>();

        services.AddHttpClient<IVerificadorPasswordFiltrada, HibpVerificadorPasswordFiltrada>();

        return services;
    }
}
