using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.CompilerServices;

#nullable enable
#pragma warning disable IDE0130
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
    public static readonly DependencyProperty CursorTypeProperty =
        DependencyProperty.RegisterAttached("CursorType", typeof(InputSystemCursorShape),
                                            typeof(UIElement), new PropertyMetadata(InputSystemCursorShape.Arrow));

    public static InputSystemCursorShape GetCursorType(DependencyObject obj) => (InputSystemCursorShape)obj.GetValue(CursorTypeProperty);

    public static void SetCursorType(DependencyObject obj, InputSystemCursorShape value)
    {
        InputSystemCursor? cursor = InputSystemCursor.Create(value);
        if (cursor is null ||
            obj is not UIElement asElement)
        {
            return;
        }

        asElement.SetCursor(cursor);
        asElement.SetValue(CursorTypeProperty, value);
    }

    extension(Control source)
    {
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetTemplateChild")]
        private extern DependencyObject GetTemplateChildAccessor(string name);

        internal T GetTemplateChild<T>(string name)
            where T : class
        {
            DependencyObject obj = source.GetTemplateChildAccessor(name);
            if (obj is not T castObj)
            {
                throw new
                    InvalidCastException($"Cannot cast type to: {typeof(T).Name} as the object expects type: {obj.GetType().Name}");
            }

            return castObj;
        }
    }
}
