using System.Collections;
using System;
using System.Diagnostics.Contracts;
using System.Diagnostics.CodeAnalysis;

namespace Fitbie.BlackboardTable;

public class Blackboard<TKey, TValue>
{
    private struct Entry // Holds actual data of Blackboard.
    {
        public int hashCode; // Lower 31 bits of hash code, -1 if unused.
        public int next; // Index of next entry, -1 if last.
        public TKey? key; // Key of entry.
        public TValue? value; // Value of entry.

        private LinkedList<TValue> values; // Backing field for initialization.
        public LinkedList<TValue> Values  // Linked values of entry.
        {
            get
            {
                values ??= new();
                return values;
            }
        }

    }

    private int[]? buckets; // Each bucket store idx of its' entry. Collided Entries chained by 'next' field index. 
    private Entry[] entries; // Holds all data about items and keys. In case of collision - connected via 'next'.

    // Determines FIFO / LIFO logic. Depends on this value we either add values to the start of LinkedList or the end.
    //  We always get values from the end of LinkedList.
    private bool fifo;

    private int count; // Total amount of elements in blackboard.
    private int version; // To prevent collection changing while enumerating.
    private int freeList; // Idx of first free entry. Free entries index field points to next free entry.
    private int freeCount; // Count of all free entries.
    private IEqualityComparer<TKey> comparer; // For EqualityComparer.Default optimization to use IEquatable if there is one.
    private object _syncRoot;


    public Blackboard() : this(0, null, true) {}

    public Blackboard(int capacity) : this(capacity, null, true) {}

    public Blackboard(IEqualityComparer<TKey> comparer) : this(0, comparer, true) {}

    // public Blackboard(IDictionary<TKey,TValue> dictionary) : this(dictionary, null) {}

    // public Blackboard(IDictionary<TKey,TValue>? dictionary, IEqualityComparer<TKey>? comparer) :
    //     this(dictionary != null ? dictionary.Count: 0, comparer, true) 
    // {
    //     ArgumentNullException.ThrowIfNull(dictionary);

    //     foreach (KeyValuePair<TKey,TValue> pair in dictionary)
    //     {
    //         Add(pair.Key, pair.Value);
    //     }
    // }

     
    public Blackboard(int capacity, IEqualityComparer<TKey>? comparer, bool fifo) 
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        
        Initialize(capacity);
        this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        this.fifo = fifo;
    }

        
    // public IEqualityComparer<TKey> Comparer => comparer;
    
    // public int Count => count - freeCount;

    // // ICollection<TKey> IDictionary<TKey, TValue>.Keys {
    // //     get {                
    // //         keys ??= new KeyCollection(this);                
    // //         return keys;
    // //     }
    // // }

    // // IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys {
    // //     get {                
    // //         if (keys == null) keys = new KeyCollection(this);                
    // //         return keys;
    // //     }
    // // }

    // // public ValueCollection Values {
    // //     get {
    // //         Contract.Ensures(Contract.Result<ValueCollection>() != null);
    // //         if (values == null) values = new ValueCollection(this);
    // //         return values;
    // //     }
    // // }

    // // ICollection<TValue> IDictionary<TKey, TValue>.Values {
    // //     get {                
    // //         if (values == null) values = new ValueCollection(this);
    // //         return values;
    // //     }
    // // }

    // // IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values {
    // //     get {                
    // //         if (values == null) values = new ValueCollection(this);
    // //         return values;
    // //     }
    // // }

    // bool IDictionary<TKey, TValue>.Remove(TKey key) 
    // {
    //     return Detach(key) == null;
    // }

    // TValue IDictionary<TKey, TValue>.this[TKey key]
    // {
    //     get 
    //     {
    //         int i = FindEntry(key);
    //         if (i >= 0) 
    //         {
    //             // If First In First Out - return value from first node, otherwise - last.
    //             return fifo ? entries[i].Values.GetRemoveFirst() : entries[i].Values.GetRemoveLast();
    //         }

    //         throw new KeyNotFoundException();
    //     }

    //     set => Pin(key, value);
    // }

    // void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
    // {
    //     Pin(key, value);
    // }

    // void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
    // {
    //     Pin(keyValuePair.Key, keyValuePair.Value);
    // }

    // bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
    // {
    //     int i = FindEntry(keyValuePair.Key);
    //     if(i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value)) {
    //         return true;
    //     }
    //     return false;
    // }

    // bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
    // {
    //     int i = FindEntry(keyValuePair.Key);
    //     if( i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value))
    //     {
    //         Remove(keyValuePair.Key);
    //         return true;
    //     }
    //     return false;
    // }

    // public void Clear() {
    //     if (count > 0) {
    //         for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
    //         Array.Clear(entries, 0, count);
    //         freeList = -1;
    //         count = 0;
    //         freeCount = 0;
    //         version++;
    //     }
    // }

    // public bool ContainsKey(TKey key) {
    //     return FindEntry(key) >= 0;
    // }

    // public bool ContainsValue(TValue value) {
    //     if (value == null) {
    //         for (int i = 0; i < count; i++) {
    //             if (entries[i].hashCode >= 0 && entries[i].value == null) return true;
    //         }
    //     }
    //     else {
    //         EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
    //         for (int i = 0; i < count; i++) {
    //             if (entries[i].hashCode >= 0 && c.Equals(entries[i].value, value)) return true;
    //         }
    //     }
    //     return false;
    // }

    // private void CopyTo(KeyValuePair<TKey,TValue>[] array, int index) {
    //     if (array == null) {
    //         ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
    //     }
        
    //     if (index < 0 || index > array.Length ) {
    //         ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
    //     }

    //     if (array.Length - index < Count) {
    //         ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
    //     }

    //     int count = this.count;
    //     Entry[] entries = this.entries;
    //     for (int i = 0; i < count; i++) {
    //         if (entries[i].hashCode >= 0) {
    //             array[index++] = new KeyValuePair<TKey,TValue>(entries[i].key, entries[i].value);
    //         }
    //     }
    // }


    // public Enumerator GetEnumerator() {
    //     return new Enumerator(this, Enumerator.KeyValuePair);
    // }


    // IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
    //     return new Enumerator(this, Enumerator.KeyValuePair);
    // }        


    // bool ICollection<KeyValuePair<TKey,TValue>>.IsReadOnly => false;

    // void ICollection<KeyValuePair<TKey,TValue>>.CopyTo(KeyValuePair<TKey,TValue>[] array, int index) => CopyTo(array, index);

    // void ICollection.CopyTo(Array array, int index) {
    //     if (array == null) {
    //         ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
    //     }
        
    //     if (array.Rank != 1) {
    //         ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
    //     }

    //     if( array.GetLowerBound(0) != 0 ) {
    //         ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
    //     }
        
    //     if (index < 0 || index > array.Length) {
    //         ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
    //     }

    //     if (array.Length - index < Count) {
    //         ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
    //     }
        
    //     KeyValuePair<TKey,TValue>[] pairs = array as KeyValuePair<TKey,TValue>[];
    //     if (pairs != null) {
    //         CopyTo(pairs, index);
    //     }
    //     else if( array is DictionaryEntry[]) {
    //         DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
    //         Entry[] entries = this.entries;
    //         for (int i = 0; i < count; i++) {
    //             if (entries[i].hashCode >= 0) {
    //                 dictEntryArray[index++] = new DictionaryEntry(entries[i].key, entries[i].value);
    //             }
    //         }                
    //     }
    //     else {
    //         object[] objects = array as object[];
    //         if (objects == null) {
    //             ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
    //         }

    //         try {
    //             int count = this.count;
    //             Entry[] entries = this.entries;
    //             for (int i = 0; i < count; i++) {
    //                 if (entries[i].hashCode >= 0) {
    //                     objects[index++] = new KeyValuePair<TKey,TValue>(entries[i].key, entries[i].value);
    //                 }
    //             }
    //         }
    //         catch(ArrayTypeMismatchException) {
    //             ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
    //         }
    //     }
    // }

    // IEnumerator IEnumerable.GetEnumerator() {
    //     return new Enumerator(this, Enumerator.KeyValuePair);
    // }

    // bool ICollection.IsSynchronized {
    //     get { return false; }
    // }

    // object ICollection.SyncRoot { 
    //     get { 
    //         if( _syncRoot == null) {
    //             System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);    
    //         }
    //         return _syncRoot; 
    //     }
    // }

    // bool IDictionary.IsFixedSize {
    //     get { return false; }
    // }

    // bool IDictionary.IsReadOnly {
    //     get { return false; }
    // }

    // ICollection IDictionary.Keys {
    //     get { return (ICollection)Keys; }
    // }

    // ICollection IDictionary.Values {
    //     get { return (ICollection)Values; }
    // }


    // object IDictionary.this[object key] {
    //     get { 
    //         if( IsCompatibleKey(key)) {                
    //             int i = FindEntry((TKey)key);
    //             if (i >= 0) { 
    //                 return entries[i].value;                
    //             }
    //         }
    //         return null;
    //     }
    //     set {                 
    //         if (key == null)
    //         {
    //             ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
    //         }
    //         ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

    //         try {
    //             TKey tempKey = (TKey)key;
    //             try {
    //                 this[tempKey] = (TValue)value; 
    //             }
    //             catch (InvalidCastException) { 
    //                 ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));   
    //             }
    //         }
    //         catch (InvalidCastException) { 
    //             ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
    //         }
    //     }
    // }


    // private static bool IsCompatibleKey(object key) {
    //     if( key == null) {
    //             ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
    //         }
    //     return (key is TKey); 
    // }


    // void IDictionary.Add(object key, object value) {            
    //     if (key == null)
    //     {
    //         ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
    //     }
    //     ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

    //     try {
    //         TKey tempKey = (TKey)key;

    //         try {
    //             Add(tempKey, (TValue)value);
    //         }
    //         catch (InvalidCastException) { 
    //             ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));   
    //         }
    //     }
    //     catch (InvalidCastException) { 
    //         ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
    //     }
    // }

    // bool IDictionary.Contains(object key) {    
    //     if(IsCompatibleKey(key)) {
    //         return ContainsKey((TKey)key);
    //     }
    
    //     return false;
    // }

    // IDictionaryEnumerator IDictionary.GetEnumerator() {
    //     return new Enumerator(this, Enumerator.DictEntry);
    // }

    // void IDictionary.Remove(object key) 
    // {            
    //     if(IsCompatibleKey(key)) {
    //         Remove((TKey)key);
    //     }
    // }
    

    public virtual void Pin(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (buckets == null) 
        { 
            Initialize(0);
        }

        int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF; // AND to prevent negative hashcode.
        int targetBucket = hashCode % buckets!.Length;

        for (int i = buckets[targetBucket]; i >= 0; i = entries[i].next) 
        {
            if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
            {
                entries[i].Values.AddTo(value, fifo);
                version++;
                return;
            }
        }

        int index;
        if (freeCount > 0) 
        {
            index = freeList;
            freeList = entries[index].next;
            freeCount--;
        }
        else 
        {
            if (count == entries.Length)
            {
                Resize();
                targetBucket = hashCode % buckets.Length;
            }
            index = count;
            count++;
        }

        entries[index].hashCode = hashCode;
        entries[index].next = buckets[targetBucket];
        entries[index].key = key;
        entries[index].Values.AddTo(value, fifo);
        buckets[targetBucket] = index;
        version++;

// #if FEATURE_RANDOMIZED_STRING_HASHING

// #if FEATURE_CORECLR
//         // In case we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
//         // in this case will be EqualityComparer<string>.Default.
//         // Note, randomized string hashing is turned on by default on coreclr so EqualityComparer<string>.Default will 
//         // be using randomized string hashing

//         if (collisionCount > HashHelpers.HashCollisionThreshold && comparer == NonRandomizedStringEqualityComparer.Default) 
//         {
//             comparer = (IEqualityComparer<TKey>) EqualityComparer<string>.Default;
//             Resize(entries.Length, true);
//         }
// #else
//         if(collisionCount > HashHelpers.HashCollisionThreshold && HashHelpers.IsWellKnownEqualityComparer(comparer)) 
//         {
//             comparer = (IEqualityComparer<TKey>) HashHelpers.GetRandomizedEqualityComparer(comparer);
//             Resize(entries.Length, true);
//         }
// #endif // FEATURE_CORECLR

// #endif

    }

    public TValue? Peek(TKey key)
    {
        
    }

    public bool TryDetach(TKey key, [NotNullWhen(true)] out TValue? result)
    {
        result = default;

        int entry = FindEntry(key);
        if (entry >= 0)
        {
            result = Detach(key)!;
            return true;
        }
        
        return false;
    }


    public TValue? Detach(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (buckets == null) { return default; }

        int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
        int bucket = hashCode % buckets.Length;
        int lastEntry = -1;
        for (int i = buckets[bucket]; i >= 0; lastEntry = i, i = entries[i].next)
        {
            if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
            {
                var result = entries[i].Values.GetRemoveLast(); // We always remove last, bc LIFO-FIFO logic determined by Pin().

                if (entries[i].Values.Count <= 0) // Clear entry only if we take last element from its' linked list.
                {
                    if (lastEntry < 0) // If we did NOT traverse any entries yet.
                    {
                        buckets[bucket] = entries[i].next; // We need to update bucket array only if we NOT traverse any entry.
                    }
                    else // We traverse entry, so we put next(from current) entry index into 'next' field of previous entry. 
                    {
                        // We don't need to update bucket value, because we already traverse n buckets.
                        entries[lastEntry].next = entries[i].next;
                    }
                

                    entries[i].hashCode = -1;
                    entries[i].key = default;
                    entries[i].value = default;

                    // Now freelist points to current entry.
                    entries[i].next = freeList;
                    freeList = i;
                    freeCount++;
                    version++;
                }
                
                return result;
            }
        }

        return default;
    }


    public bool TryGetValue(TKey key, out TValue value) {
        int i = FindEntry(key);
        if (i >= 0) {
            value = entries[i].value;
            return true;
        }
        value = default;
        return false;
    }


    [MemberNotNull(nameof(entries))]
    protected virtual void Initialize(int capacity)
    {
        int size = PrimeHelper.GetPrime(capacity);
        buckets = new int[size];
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = -1;
        }

        entries = new Entry[size];
        freeList = -1;
    }


    private int FindEntry(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (buckets != null)
        {
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            for (int i = buckets[hashCode % buckets.Length]; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) return i;
            }
        }
        return -1;
    }


    private void Resize()
    {
        Resize(PrimeHelper.ExpandPrime(count), false);
    }


    private void Resize(int newSize, bool forceNewHashCodes)
    {
        Contract.Assert(newSize >= entries.Length);
        int[] newBuckets = new int[newSize];
        for (int i = 0; i < newBuckets.Length; i++) 
        {
            newBuckets[i] = -1;
        }

        Entry[] newEntries = new Entry[newSize];
        Array.Copy(entries, 0, newEntries, 0, count);
        if(forceNewHashCodes) 
        {
            for (int i = 0; i < count; i++)
            {
                if(newEntries[i].hashCode != -1) 
                {
                    newEntries[i].hashCode = (comparer.GetHashCode(newEntries[i].key) & 0x7FFFFFFF);
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (newEntries[i].hashCode >= 0) 
            {
                int bucket = newEntries[i].hashCode % newSize;
                newEntries[i].next = newBuckets[bucket];
                newBuckets[bucket] = i;
            }
        }
        buckets = newBuckets;
        entries = newEntries;
    }

}
