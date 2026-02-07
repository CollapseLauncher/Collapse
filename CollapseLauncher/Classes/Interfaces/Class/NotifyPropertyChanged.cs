using CollapseLauncher.Extension;
using System.ComponentModel;
using System.Runtime.CompilerServices;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Interfaces.Class;

public abstract class NotifyPropertyChanged : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _propertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        bool hasThreadAccess = DispatcherQueueExtensions.HasThreadAccessSafe();
        if (hasThreadAccess)
        {
            Impl();
            return;
        }

        DispatcherQueueExtensions.TryEnqueue(Impl);
        return;

        void Impl() => _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}