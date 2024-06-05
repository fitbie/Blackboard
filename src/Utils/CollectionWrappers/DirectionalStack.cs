namespace Fitbie.BlackboardTable;

internal class DirectionalStack<T> : Stack<T>, IDirectionalCollection<T>
{
    public void Put(T item) => Push(item);

    public T Take() => Pop();

    public bool TryTake(out T? result) => TryPop(out result);

}
