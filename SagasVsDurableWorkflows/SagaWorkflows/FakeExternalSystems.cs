namespace SagaWorkflows;

public static class FakeExternalSystems
{
    /// <summary>
    /// The systems the workflows talk to (a bank, airlines, an LLM...), as in-memory fakes. The tests reach them
    /// through the container to arrange failures
    /// </summary>
    public static IServiceCollection AddFakeExternalSystems(this IServiceCollection services)
    {
        services.AddSingleton<MoneyTransfer.Bank>();
        services.AddSingleton<TripBooking.TravelProviders>();
        services.AddSingleton<OrderApproval.Storefront>();
        services.AddSingleton<Subscriptions.Billing>();
        services.AddSingleton<FanOutFanIn.BackupStorage>();
        return services;
    }
}
