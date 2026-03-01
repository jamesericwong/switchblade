using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Synchronizes an <see cref="ObservableCollection{T}"/> with a source list in-place,
    /// preserving object identity and minimizing UI change notifications.
    /// Uses a two-pointer algorithm for O(N) complexity.
    /// </summary>
    public static class ObservableCollectionSync
    {
        /// <summary>
        /// Synchronizes <paramref name="collection"/> to match <paramref name="source"/>
        /// while preserving existing object references and minimizing move/insert/remove operations.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="collection">The observable collection to sync (mutated in-place).</param>
        /// <param name="source">The authoritative source list defining the desired order and content.</param>
        public static void Sync<T>(ObservableCollection<T> collection, IList<T> source)
        {
            // Phase 1: Remove items not in source
            var sourceSet = new HashSet<T>(source);
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!sourceSet.Contains(collection[i]))
                    collection.RemoveAt(i);
            }

            // Phase 2: Two-pointer sync for ordering and insertion
            int ptr = 0;
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (ptr < collection.Count && EqualityComparer<T>.Default.Equals(collection[ptr], item))
                {
                    ptr++;
                }
                else
                {
                    int foundAt = -1;
                    for (int j = ptr + 1; j < collection.Count; j++)
                    {
                        if (EqualityComparer<T>.Default.Equals(collection[j], item))
                        {
                            foundAt = j;
                            break;
                        }
                    }

                    if (foundAt != -1)
                    {
                        collection.Move(foundAt, ptr);
                    }
                    else
                    {
                        collection.Insert(ptr, item);
                    }
                    ptr++;
                }
            }
        }
    }
}
