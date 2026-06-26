namespace CTSHIPDashboard.Services
{
    public static class ProgramServicesDeathRegisterAdditions
    {
        public static IServiceCollection AddDeathRegisterManagementServices(this IServiceCollection services)
        {
            services.AddScoped<IDeathRegisterService, DeathRegisterService>();
            return services;
        }
    }

}
