using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Filters;
using MPM.Modules.Propuestas.Services;

namespace MPM.Modules.Propuestas;

public static class ModuleRegistration
{
    public static IServiceCollection AddPropuestasModule(this IServiceCollection services)
    {
        services.AddScoped<PropuestasHandler>();
        services.AddScoped<PropuestasExceptionFilter>();
        services.AddScoped<PropuestasCatalogoService>();
        services.AddScoped<CensusCertificationSyncService>();
        services.AddScoped<PropuestasRecomendacionService>();
        services.AddSingleton<ProposalTemplateProvider>();
        services.AddScoped<IProposalLicitacionLookup, ProposalLicitacionLookup>();
        services.AddScoped<ICertificationFileProvider, CensusCertificationFileProvider>();
        services.AddScoped<IProposalSummaryProvider, AnalisisProposalSummaryProvider>();
        services.AddScoped<DocxProposalGenerator>();
        services.AddScoped<IPropuestaService, PropuestaService>();
        services.AddScoped<IGoogleDriveService, GoogleDriveService>();
        services.AddScoped<IDecisionAvisoNotifier, DecisionAvisoNotifier>();
        services.AddScoped<IDecisionAvisoService, DecisionAvisoService>();
        return services;
    }
}
