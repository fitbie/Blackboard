﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Fitbie.BlackboardTable;

public class Blackboard<TKey, TValue> : ICollection<BlackboardPair<TKey, TValue>>
{
    private struct Entry // Holds actual data of Blackboard.
    {
        public int hashCode; // Lower 31 bits of hash code, -1 if unused.
        public int next; // Index of next entry, -1 if last.
        public TKey key; // Key of entry.

        public IDirectionalCollection<TValue> Values;

        public Entry(bool fifo)
        {
            Values = fifo ? new DirectionalQueue<TValue>() : new DirectionalStack<TValue>();
        }
    }

    private int[]? buckets; // Each bucket store idx of its' entry. Collided Entries chained by 'next' field index. 
    private Entry[] entries; // Holds all data about items and keys. In case of collision - connected via 'next'.

    // Determines FIFO / LIFO logic. Depends on this value we either add values to the start of LinkedList or the end.
    //  We always get values from the end of LinkedList.
    private readonly bool fifo = true;

    private int count; // Total amount of existing entries in blackboard (including freeCount).
    private int version; // To prevent collection changing while enumerating.
    private int freeList; // Idx of first free entry. Free entries index field points to next free entry.
    private int freeCount; // Count of all free entries.
    private readonly IEqualityComparer<TKey> comparer; // For EqualityComparer.Default optimization to use IEquatable if there is one.
    private object? _syncRoot;

    public IEqualityComparer<TKey> Comparer => comparer;


    #region Constructors

    public Blackboard(bool fifo) : this(fifo, 0, null) {}

    public Blackboard(bool fifo, int capacity) : this(fifo, capacity, null) {}

    public Blackboard(bool fifo, IEqualityComparer<TKey> comparer) : this(fifo, 0, comparer) {}

    public Blackboard(bool fifo, IDictionary<TKey,TValue>? dictionary, IEqualityComparer<TKey>? comparer) :
        this(fifo, dictionary != null ? dictionary.Count : 0, comparer) 
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        foreach (KeyValuePair<TKey,TValue> pair in dictionary)
        {
            Pin(pair.Key, pair.Value);
        }
    }
    
    public Blackboard(Blackboard<TKey, TValue> blackboard) :
        this(blackboard.fifo, blackboard.entries.Length, null) 
    {
        for (int i = 0; i < blackboard.entries.Length; i++)
        {
            while (blackboard.entries[i].Values.Count > 0)
            {
                Pin(blackboard.entries[i].key, blackboard.entries[i].Values.Take());
            }
        }
    }
     
    public Blackboard(bool fifo, int capacity, IEqualityComparer<TKey>? comparer) 
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        
        this.fifo = fifo;
        this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        Initialize(capacity);
    }

    #endregion

    #region Interfaces
    
    public int Count => count - freeCount;

    bool ICollection<BlackboardPair<TKey,TValue>>.IsReadOnly => false;


    void ICollection<BlackboardPair<TKey, TValue>>.Add(BlackboardPair<TKey, TValue> blackboardPair)
    {
        int entryIdx = FindEntry(blackboardPair.Key);
        if (entryIdx > -1)
        {
            
        }
    }


    bool ICollection<BlackboardPair<TKey, TValue>>.Contains(BlackboardPair<TKey, TValue> blackboardPair)
    {
        int entryIdx = FindEntry(blackboardPair.Key);
        if (entryIdx > -1)
        {
            return blackboardPair.Equals(new(entries[entryIdx].key, entries[entryIdx].Values));
        }

        return false;
    }


    bool ICollection<BlackboardPair<TKey, TValue>>.Remove(BlackboardPair<TKey, TValue> blackboardPair)
    {
        return TryDetach(blackboardPair.Key, out var _);
    }


    public void Clear() {
        if (count > 0) {
            for (int i = 0; i < buckets.Length; i++) { buckets[i] = -1; }
            Array.Clear(entries, 0, count);
            freeList = -1;
            count = 0;
            freeCount = 0;
            version++;
        }
    }


    public void CopyTo(BlackboardPair<TKey,TValue>[] array, int index) 
    {
        ArgumentNullException.ThrowIfNull(array);
        
        if (index < 0 || index > array.Length ) 
        {
            throw new IndexOutOfRangeException();
        }

        if (array.Length - index < Count)
        {
            throw new ArgumentException("Destination array was not long enough. Check the destination index, length, and the array's lower bounds.", nameof(array));
        }

        int count = this.count;
        Entry[] entries = this.entries;
        for (int i = 0; i < count; i++) 
        {
            if (entries[i].hashCode >= 0)
            {
                array[index++] = new BlackboardPair<TKey, TValue>(entries[i].key, entries[i].Values);
            }
        }
    }


    public IEnumerator<BlackboardPair<TKey, TValue>> GetEnumerator()
    {
        return new Enumerator(this, version);
    }


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


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
    
    #endregion
    

    [MemberNotNull(nameof(entries))]
    protected virtual void Initialize(int capacity)
    {
        int size = PrimeHelper.GetPrime(capacity);
        buckets = new int[size];
        entries = new Entry[size];
        for (int i = 0; i < size; i++)
        {
            buckets[i] = -1;
            entries[i] = new(fifo);
        }
        
        freeList = -1;
    }


    private void Resize()
    {
        Resize(PrimeHelper.ExpandPrime(count), false);
    }


    private void Resize(int newSize, bool forceNewHashCodes)
    {
        int[] newBuckets = new int[newSize];
        for (int i = 0; i < newBuckets.Length; i++) 
        {
            newBuckets[i] = -1;
        }

        Entry[] newEntries = new Entry[newSize];
        Array.Copy(entries, 0, newEntries, 0, count);
        for (int i = count; i < newSize; i++)
        {
            newEntries[i] = new(fifo);
        }

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
                entries[i].Values.Put(value);
                version++;
                return;
            }
        }

        int index;
        if (freeCount > 0) 
        {
            index = freeList;
            freeList = entries[freeList].next;
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
        entries[index].Values.Put(value);
        buckets[targetBucket] = index;
        version++;


//         // The point of Randomized String Hashing is when some bad guys send many string requests with the same hashcode to Web Service
//         // for example, it will fall down while traversing all entries[] because of hashcode collisions. Randomized string hashing
//         // prevent us from this.
// #if FEATURE_RANDOMIZED_STRING_HASHING

// #if FEATURE_CORECLR
//         // In case we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
//         // in this case will be EqualityComparer<string>.Default.
//         // Note, randomized string hashing is turned on by default on coreclr so EqualityComparer<string>.Default will 
//         // be using randomized string hashing.

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

    public bool TryPeek(TKey key, [NotNullWhen(true)]out TValue? result)
    {
        int entryIdx = FindEntry(key);
        if (entryIdx > -1)
        {
            return entries[entryIdx].Values.TryPeek(out result);
        }

        result = default;
        return false;
    }


    public TValue? Peek(TKey key)
    {
        int entryIdx = FindEntry(key);
        if (entryIdx > -1)
        {
            return entries[entryIdx].Values.Peek();
        }

        return default;
    }


    public bool TryDetach(TKey key, [NotNullWhen(true)] out TValue? result)
    {
        int entry = FindEntry(key);
        if (entry >= 0)
        {
            result = Detach(key)!;
            return true;
        }
        
        result = default;
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
                var result = entries[i].Values.Take(); // We always remove last, bc LIFO-FIFO logic determined by Pin().

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


    internal struct Enumerator : IEnumerator<BlackboardPair<TKey, TValue>>
    {
        private Blackboard<TKey, TValue> blackboard;
        private int version; // To prevent Blacboard changing while enumerating.
        private int index;
        private BlackboardPair<TKey, TValue> current;

        public Enumerator(Blackboard<TKey, TValue> blackboard, int version)
        {
            this.blackboard = blackboard;
            this.version = version;
        }

        public BlackboardPair<TKey, TValue> Current => current;
        object IEnumerator.Current => current;


         public bool MoveNext() 
        {
            if (version != blackboard.version) 
            {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }

            // Use unsigned comparison since we set index to blackboard.count+1 when the enumeration ends.
            // blackboard.count+1 could be negative if dictionary.count is Int32.MaxValue.
            while ((uint)index < (uint)blackboard.count) 
            {
                if (blackboard.entries[index].hashCode >= 0) {
                    current = new BlackboardPair<TKey, TValue>(blackboard.entries[index].key, blackboard.entries[index].Values);
                    index++;
                    return true;
                }

                index++;
            }

            index = blackboard.count + 1;
            current = new();
            return false;
        }

        public void Reset()
        {
            if (version != blackboard.version)
            {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }

            index = 0;
            current = new();
        }


        public void Dispose() {}
    }

}
