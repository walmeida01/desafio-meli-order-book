using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBook.Infrastructure.Postgres;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Infrastructure.Observability;

namespace OrderBook.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresConnectionSettings>(configuration.GetSection(PostgresConnectionSettings.SectionName));
        services.AddSingleton<PostgresConnectionFactory>();
        services.AddSingleton<RuntimeReadiness>();
        services.AddSingleton<AdmissionState>();
        services.AddSingleton<OrderCommandQueue>();
        services.AddSingleton<OrderBookMetrics>();
        services.AddSingleton<IReadiness>(sp => sp.GetRequiredService<RuntimeReadiness>());
        services.AddSingleton<MigrationRunner>();
        services.AddScoped<IOrderSequence, PostgresOrderSequence>();
        services.AddScoped<PostgresUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PostgresUnitOfWork>());
        services.AddScoped<IOrderRepository, PostgresOrderRepository>();
        services.AddScoped<IWalletRepository, PostgresWalletRepository>();
        services.AddScoped<IReservationRepository, PostgresReservationRepository>();
        services.AddScoped<ITradeRepository, PostgresTradeRepository>();
        services.AddScoped<ILedgerRepository, PostgresLedgerRepository>();
        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        services.AddScoped<PostgresTradeDuplicateChecker>();
        services.AddScoped<IOrderQueries, PostgresOrderQueries>();
        services.AddSingleton<OrderBookRecovery>();
        services.AddSingleton<global::OrderBook.Domain.Modules.Matching.OrderBook>();
        services.AddSingleton<PostgresOrderSubmission>();
        services.AddSingleton<global::OrderBook.Application.Modules.Orders.SubmitOrder.ISubmitOrder>(sp => sp.GetRequiredService<PostgresOrderSubmission>());
        services.AddHostedService<OrderCommandWorker>();
        services.AddSingleton<PostgresWriterOwnership>();
        services.AddHostedService<WriterRuntime>();
        return services;
    }
}
