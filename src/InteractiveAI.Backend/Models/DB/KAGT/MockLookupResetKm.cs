using System;
using System.Collections.Generic;
using System.Linq;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT
{

    public static class MockLookupResetKm
    {

        public sealed record Item(int Id, decimal Km);


        public static readonly IReadOnlyList<Item> Items = new List<Item>
        {
            new(1763,  12500m),
            new(1764,  25000m),
            new(1840,  37500m),
            new(1841,  50000m),
            new(1854,  62500m),
            new(1856,  75000m),
            new(1857,  87500m),
            new(1895, 100000m),
            new(2117, 112500m),
            new(2118, 125000m),
            new(2119, 150000m),
        };


        public static Dictionary<int, decimal> ToDictionary()
            => Items.ToDictionary(x => x.Id, x => x.Km);


        public static decimal? Get(int? id)
            => id is null ? null : Items.FirstOrDefault(x => x.Id == id.Value)?.Km;
    }
}