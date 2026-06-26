using CTSHIPDashboard.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ProgramServicesAdditions
{
    public static IServiceCollection AddReferralManagementServices(this IServiceCollection services)
    {
        services.AddScoped<IReferralService, ReferralService>();
        return services;
    }
}
