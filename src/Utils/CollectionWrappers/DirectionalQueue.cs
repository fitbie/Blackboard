using System.Collections;

namespace Fitbie.BlackboardTable;

internal class DirectionalQueue<T> : Queue<T>, IDirectionalCollection<T>
{
    public void Put(T item) => Enqueue(item);

    public T Take() => Dequeue();

    public bool TryTake(out T? result) => TryDequeue(out result);
}
