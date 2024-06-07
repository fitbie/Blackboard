namespace Fitbie.BlackboardTable;

public struct BlackboardPair<TKey, TValue> : IEquatable<BlackboardPair<TKey, TValue>>
{
    public readonly TKey Key;
    public readonly IEnumerable<TValue> Values;


    public BlackboardPair(TKey key, IEnumerable<TValue> values)
    {
        Key = key;
        Values = values;
    }


    public bool Equals(BlackboardPair<TKey, TValue> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        IEqualityComparer<TKey> keyComparer = EqualityComparer<TKey>.Default;
        if (!keyComparer.Equals(Key, other.Key))
        {
            return false;
        }

        return Values.SequenceEqual(other.Values, EqualityComparer<TValue>.Default);
    }
}