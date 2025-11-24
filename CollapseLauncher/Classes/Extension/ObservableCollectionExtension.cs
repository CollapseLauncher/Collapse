using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CollapseLauncher.Extension
{
    /// <summary>
    /// Provides extension methods for <see cref="ObservableCollection{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    internal static class ObservableCollectionExtension<T>
    {
        /// <summary>
        /// Gets the backing list of the specified collection.
        /// </summary>
        /// <param name="source">The collection to get the backing list from.</param>
        /// <returns>A reference to the backing list of the collection.</returns>
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "items")]
        internal static extern ref IList<T> GetBackedCollectionList(Collection<T> source);

        /// <summary>
        /// Invokes the OnCountPropertyChanged method on the specified observable collection.
        /// </summary>
        /// <param name="source">The observable collection to invoke the method on.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnCountPropertyChanged")]
        private static extern void OnCountPropertyChanged(ObservableCollection<T> source);

        /// <summary>
        /// Invokes the OnIndexerPropertyChanged method on the specified observable collection.
        /// </summary>
        /// <param name="source">The observable collection to invoke the method on.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnIndexerPropertyChanged")]
        private static extern void OnIndexerPropertyChanged(ObservableCollection<T> source);

        /// <summary>
        /// Invokes the OnCollectionChanged method on the specified observable collection.
        /// </summary>
        /// <param name="source">The observable collection to invoke the method on.</param>
        /// <param name="e">The event arguments for the collection changed event.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnCollectionChanged")]
        private static extern void OnCollectionChanged(ObservableCollection<T> source, NotifyCollectionChangedEventArgs e);

        /// <summary>
        /// Refreshes all events for the specified observable collection.
        /// </summary>
        /// <param name="source">The observable collection to invoke the method on.</param>
        internal static void RefreshAllEvents(ObservableCollection<T> source)
        {
            OnCountPropertyChanged(source);
            OnIndexerPropertyChanged(source);
            OnCollectionChanged(source, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Removes a range of items from the specified observable collection quickly.
        /// </summary>
        /// <param name="sourceRange">The list of items to remove from the target collection.</param>
        /// <param name="target">The observable collection from which the items will be removed.</param>
        /// <exception cref="InvalidCastException">Thrown when the backing list of the target collection cannot be cast to a List{T}.</exception>
        /// <remarks>
        /// This method directly manipulates the backing list of the observable collection to remove the specified items,
        /// and then fires the necessary property changed and collection changed events to update any bindings.
        /// </remarks>
        internal static void RemoveItemsFast(List<T> sourceRange, ObservableCollection<T> target)
        {
            // Get the backed list instance of the collection
            List<T> targetBackedList = GetBackedCollectionList(target) as List<T> ?? throw new InvalidCastException();

            // Get the count and iterate the reference of the T from the source range
            ReadOnlySpan<T> sourceRangeSpan = CollectionsMarshal.AsSpan(sourceRange);
            int len = sourceRangeSpan.Length - 1;
            for (; len >= 0; len--)
            {
                // Remove the reference of the item T from the target backed list
                _ = targetBackedList.Remove(sourceRangeSpan[len]);
            }

            // Fire the changes event
            RefreshAllEvents(target);
        }
    }
}
