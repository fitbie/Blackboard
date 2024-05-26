namespace Fitbie.BlackboardTable;

internal static class LinkedListExtensions
{
    /// <summary>
    /// Gets <see cref="LinkedList{T}.First"/> node's value and remove node.
    /// </summary>
    /// <returns>Value of first node in list.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetRemoveFirst<T>(this LinkedList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source?.First, "LinkedListNode");

        T result = source.First.Value;
        source.RemoveFirst();
        return result;
    }


    /// <summary>
    /// Gets <see cref="LinkedList{T}.Last"/> node's value and remove node.
    /// </summary>
    /// <returns>Value of last node in list.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetRemoveLast<T>(this LinkedList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source?.Last, "LinkedListNode");

        T result = source.Last.Value;
        source.RemoveLast();
        return result;
    }
} 