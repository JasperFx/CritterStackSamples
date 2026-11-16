using Polecat;
using Polecat.Storage;

namespace PolecatIndexing;

public static class OrderIndexes
{
    public static void Configure(StoreOptions opts, bool unicodeEmail = false)
    {
        opts.Schema.For<Order>()

            // A basic index on one property of the JSON document. Stage 3 of
            // Program.cs flips the flag to index the value as nvarchar instead
            .Index(x => x.CustomerEmail, x =>
            {
                if (unicodeEmail) x.SqlType = "nvarchar(250)";
            })

            // Any depth of nesting works the same way
            .Index(x => x.ShippingAddress.PostalCode)

            // One index over two properties
            .Index(x => new { x.Region, x.Status })

            // A unique index. SQL Server will reject a second order
            // with the same order number
            .UniqueIndex(x => x.OrderNumber)

            // A covering index. City is the key, and Region and Status ride along
            // as INCLUDE columns so some queries never have to touch the table
            .Index(x => x.ShippingAddress.City, include: x => new { x.Region, x.Status })

            // Tell Polecat the real SQL type of the value, and sort it descending
            .Index(x => x.PlacedAt, x =>
            {
                x.SqlType = "datetimeoffset";
                x.SortOrder = SortOrder.Descending;
            });
    }
}
