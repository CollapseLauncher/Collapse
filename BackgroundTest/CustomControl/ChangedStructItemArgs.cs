namespace BackgroundTest.CustomControl;

public readonly struct ChangedStructItemArgs<TItem>(TItem oldItem, TItem newItem)
    where TItem : struct
{
    public TItem OldItem { get; private init; } = oldItem;
    public TItem NewItem { get; private init; } = newItem;
}

public class ChangedObjectItemArgs<TItem>(TItem? oldItem, TItem? newItem)
    where TItem : class
{
    public TItem? OldItem { get; private init; } = oldItem;
    public TItem? NewItem { get; private init; } = newItem;
}