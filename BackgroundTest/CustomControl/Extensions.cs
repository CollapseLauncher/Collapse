using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.CompilerServices;

namespace BackgroundTest.CustomControl;

public static class Extensions
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

    /// <param name="element">The <seealso cref="UIElement"/> member of an element</param>
    extension(UIElement element)
    {
        /// <summary>
        /// Set the cursor for the element.
        /// </summary>
        /// <param name="inputCursor">The cursor you want to set. Use <see cref="InputSystemCursor.Create"/> to choose the cursor you want to set.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ProtectedCursor")]
        private extern void SetCursor(InputCursor inputCursor);
    }
}
