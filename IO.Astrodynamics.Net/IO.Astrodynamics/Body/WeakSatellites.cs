// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IO.Astrodynamics.Body;

/// <summary>
/// The satellites of a <see cref="CelestialItem"/> (#676): the instances, by reference, held weakly, so that an item no
/// longer referenced elsewhere leaves the collection when it is collected, and safe across threads
/// (<see cref="ConditionalWeakTable{TKey,TValue}"/>). A live read-only view: an enumeration lists the items alive at that
/// time, in no documented order, and <see cref="Count"/> counts them.
/// </summary>
internal sealed class WeakSatellites : IReadOnlyCollection<CelestialItem>
{
    private readonly ConditionalWeakTable<CelestialItem, object> _items = new();

    /// <summary>Lists <paramref name="item"/>, once whatever the number of calls.</summary>
    internal void Add(CelestialItem item) => _items.AddOrUpdate(item, null);

    /// <summary>Removes <paramref name="item"/>, this instance only.</summary>
    internal void Remove(CelestialItem item) => _items.Remove(item);

    /// <inheritdoc />
    public int Count
    {
        get
        {
            int count = 0;
            foreach (var _ in this)
            {
                count++;
            }

            return count;
        }
    }

    /// <inheritdoc />
    public IEnumerator<CelestialItem> GetEnumerator()
    {
        foreach (var entry in (IEnumerable<KeyValuePair<CelestialItem, object>>)_items)
        {
            yield return entry.Key;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
