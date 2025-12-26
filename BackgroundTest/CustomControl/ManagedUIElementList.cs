using Microsoft.UI.Xaml;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinRT;

// ReSharper disable StaticMemberInGenericType

namespace BackgroundTest.CustomControl;

[ProjectedRuntimeClass(typeof(IList<UIElement>))]
[GeneratedBindableCustomProperty]
public partial class ManagedUIElementList : IList<UIElement>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<UIElement> _backedList;

    #region Cached Property Changes Arguments

    private static readonly PropertyChangedEventArgs CountPropertyChanged   = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");

    #endregion

    #region Events

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    /// <summary>
    /// Creates a new empty Observable List
    /// </summary>
    public ManagedUIElementList() => _backedList = [];

    /// <summary>
    /// Creates a new Observable List from the enumerable.
    /// </summary>
    /// <param name="enumerable"></param>
    /// <param name="useBorrow">Borrow the current instance of <paramref name="enumerable"/> instead of allocating new backed list if possible.</param>
    public ManagedUIElementList(IEnumerable<UIElement> enumerable, bool useBorrow = false)
    {
        if (enumerable is List<UIElement> borrowedList &&
            useBorrow)
        {
            _backedList = borrowedList;
            return;
        }

        _backedList = [.. enumerable];
    }

    /// <inheritdoc/>
    public IEnumerator<UIElement> GetEnumerator() => _backedList.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public void Add(UIElement item) => Add(item, true);

    /// <summary>
    /// Adds an item to the backed List and notify the changes.
    /// </summary>
    /// <param name="item">The object to add to the backed List.</param>
    /// <param name="notifyChanges">Whether to notify the changes or not.</param>
    public void Add(UIElement item, bool notifyChanges)
    {
        _backedList.Add(item);
        if (!notifyChanges)
        {
            return;
        }

        NotifyCountPropertyChange();
        NotifyIndexerPropertyChange();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
    }

    /// <inheritdoc/>
    public void Clear() => Clear(true);

    /// <summary>
    /// Removes all items from the backed List and notify the changes.
    /// </summary>
    /// <param name="notifyChanges">Whether to notify the changes or not.</param>
    public void Clear(bool notifyChanges)
    {
        _backedList.Clear();
        if (!notifyChanges)
        {
            return;
        }

        NotifyCountPropertyChange();
        NotifyIndexerPropertyChange();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <inheritdoc/>
    public bool Contains(UIElement item) => _backedList.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(UIElement[] array, int arrayIndex) => _backedList.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public int Count => _backedList.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public int IndexOf(UIElement item) => _backedList.IndexOf(item);

    /// <inheritdoc/>
    public bool Remove(UIElement item) => Remove(item, true);

    /// <summary>
    /// Removes the first occurrence of a specific object from the backed List.
    /// </summary>
    /// <param name="item">The object to remove from the backed List.</param>
    /// <param name="notifyChanges">Whether to notify the changes or not.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="item"/> was successfully removed from the backed List; otherwise, <see langword="false"/>.
    /// This method also returns <see langword="false"/> if <paramref name="item"/> is not found from the backed List.
    /// </returns>
    public bool Remove(UIElement item, bool notifyChanges)
    {
        bool isSuccess = _backedList.Remove(item);

        if (!isSuccess)
        {
            return false;
        }

        if (!notifyChanges)
        {
            return true;
        }

        NotifyCountPropertyChange();
        NotifyIndexerPropertyChange();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
        return true;
    }

    /// <inheritdoc/>
    public void Insert(int index, UIElement item) => Insert(index, item, true);

    /// <summary>
    /// Inserts an item to the backed List at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
    /// <param name="item">The object to insert into the backed List.</param>
    /// <param name="notifyChanges">Whether to notify the changes or not.</param>
    public void Insert(int index, UIElement item, bool notifyChanges)
    {
        _backedList.Insert(index, item);

        if (!notifyChanges)
        {
            return;
        }

        NotifyCountPropertyChange();
        NotifyIndexerPropertyChange();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <inheritdoc/>
    public void RemoveAt(int index) => RemoveAt(index, true);

    /// <summary>
    /// Removes the backed List item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <param name="notifyChanges">Whether to notify the changes or not.</param>
    public void RemoveAt(int index, bool notifyChanges)
    {
        UIElement item = _backedList[index];
        _backedList.Remove(item);

        if (!notifyChanges)
        {
            return;
        }

        NotifyCountPropertyChange();
        NotifyIndexerPropertyChange();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    /// <inheritdoc/>
    [IndexerName("Item")]
    public UIElement this[int index]
    {
        get => _backedList[index];
        set
        {
            UIElement oldItem = _backedList[index];
            UIElement newItem = _backedList[index] = value;

            NotifyIndexerPropertyChange();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, index));
        }
    }

    private void NotifyCountPropertyChange()   => PropertyChanged?.Invoke(this, CountPropertyChanged);
    private void NotifyIndexerPropertyChange() => PropertyChanged?.Invoke(this, IndexerPropertyChanged);
}
