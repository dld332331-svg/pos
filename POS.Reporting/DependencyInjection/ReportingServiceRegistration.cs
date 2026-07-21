using Microsoft.Extensions.DependencyInjection;
using POS.Application.Services;
using POS.Reporting.Exporters;

namespace POS.Reporting.DependencyInjection;

public static class ReportingServiceRegistration
{
    public static IServiceCollection AddReportingServices(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, ReportExporter>();
        return services;
    }
}
