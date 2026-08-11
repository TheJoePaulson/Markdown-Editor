using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkdownEditor.ViewModels;

/// <summary>
/// Base class for ViewModels.
/// Provides INotifyPropertyChanged plumbing and a SetProperty helper
/// so derived classes can write clean, terse properties.
/// </summary>
public abstract class BindableBase : INotifyPropertyChanged
{
    /// <summary>
    /// Raised when a property value has changed.
    /// The UI binds to this via INotifyPropertyChanged.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the field to the new value, raises PropertyChanged if it changed,
    /// and optionally invokes a callback after the change.
    /// </summary>
    /// <typeparam name="T">Field type.</typeparam>
    /// <param name="storage">Backing field passed by ref.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">
    /// Automatically supplied by the compiler.
    /// </param>
    /// <param name="onChanged">
    /// Optional callback invoked only when the value actually changed.
    /// </param>
    /// <returns>True if the value changed; otherwise false.</returns>
    protected bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null,
        Action? onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        onChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Raises PropertyChanged for the specified property.
    /// </summary>
    /// <param name="propertyName">
    /// Automatically supplied by the compiler when called from a setter.
    /// </param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyChanged for multiple properties at once.
    /// Useful when a single setter affects several derived/computed values.
    /// </summary>
    /// <param name="propertyNames">Names of properties that changed.</param>
    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        if (propertyNames == null)
        {
            return;
        }

        foreach (string name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }
}