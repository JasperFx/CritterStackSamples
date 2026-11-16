using Marten;
using Marten.Schema;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace MartenIndexing;

public static class OrderIndexes
{
    public static void Configure(StoreOptions opts, bool caseInsensitiveEmail = false)
    {
        opts.Schema.For<Order>()

            // A basic index on one property of the JSON document. Stage 3 of
            // Program.cs flips the flag to index lower(CustomerEmail) instead
            .Index(x => x.CustomerEmail, x =>
            {
                if (caseInsensitiveEmail) x.Casing = ComputedIndex.Casings.Lower;
            })

            // Any depth of nesting works the same way
            .Index(x => x.ShippingAddress.PostalCode)

            // One index over two properties. The order matters, just like
            // it does for any other composite b-tree index
            .Index(x => new { x.Region, x.Status })

            // A unique index. Marten will reject a second order
            // with the same order number
            .UniqueIndex(x => x.OrderNumber)

            // A partial index. Only the open orders are in it, so it
            // stays small no matter how many delivered orders pile up
            .Index(x => x.ShippingAddress.City, x =>
            {
                x.Name = "mt_doc_order_idx_open_by_city";
                x.Predicate = "(data ->> 'Status') = 'Open'";
            })

            // Computed indexes do not work for DateTimeOffset, so duplicate
            // the value into its own column and index that instead
            .Duplicate(x => x.PlacedAt, configure: x =>
            {
                x.SortOrder = SortOrder.Desc;
            })

            // A GIN index over the whole document...
            .GinIndexJsonData()

            // ...and a smaller one over just the Lines child collection
            .GinIndexJsonDataMember(x => x.Lines)

            // A full text index over one property, stored as GiST rather
            // than the default GIN
            .FullTextIndex(x => x.Method = IndexMethod.gist, x => x.DeliveryNotes);
    }
}
