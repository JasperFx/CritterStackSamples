using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Wallets;

public static class GetWalletsEndpoint
{
    [WolverineGet("/api/wallets")]
    public static Task<IReadOnlyList<Wallet>> Get(IQuerySession session)
        => session.Query<Wallet>().ToListAsync();
}

public static class GetWalletByIdEndpoint
{
    [WolverineGet("/api/wallets/{id}")]
    public static Wallet? Get(Guid id, [Entity] Wallet? wallet) => wallet;
}

public static class GetWalletByOwnerEndpoint
{
    [WolverineGet("/api/wallets/owner/{ownerId}")]
    public static Task<IReadOnlyList<Wallet>> Get(Guid ownerId, IQuerySession session)
        => session.Query<Wallet>().Where(w => w.OwnerId == ownerId).ToListAsync();
}
