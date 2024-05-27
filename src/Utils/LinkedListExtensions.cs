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


    /// <summary>
    /// Adds node with value to linked list depend on bool parameter <paramref name="first"/>.
    /// </summary>
    /// <param name="value">Value to add.</param>
    /// <param name="first">If true - calls <see cref="LinkedList{T}.AddFirst(T)"/>,
    /// else calls <see cref="LinkedList{T}.AddLast(T)"/>.</param>
    public static void AddTo<T>(this LinkedList<T> source, T value, bool first)
    {
        if (first) { source.AddFirst(value); }
        else { source.AddLast(value); }
    }


    
    /// <summary>
    /// Removes node and its' value from linked list depend on bool parameter <paramref name="first"/>.
    /// </summary>
    /// <param name="first">If true - calls <see cref="LinkedList{T}.RemoveFirst(T)"/>, 
    /// else calls <see cref="LinkedList{T}.RemoveLast(T)"/>.</param>
    public static T RemoveFrom<T>(this LinkedList<T> source, bool first)
    {
        if (first) { return GetRemoveFirst(source); }
        else { return GetRemoveLast(source); }
    }
} 