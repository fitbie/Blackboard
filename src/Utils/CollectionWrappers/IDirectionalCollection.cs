namespace Fitbie.BlackboardTable;


/// <summary>
/// Interface to wrap <see cref="Queue{T}"/> and <see cref="Stack{T}"/>.
/// </summary>
internal interface IDirectionalCollection<T> : IEnumerable<T>
{
    public void Put(T item);
    public T Take();
    public bool TryTake(out T? result);
    public T Peek();
    public bool TryPeek(out T? result);
}