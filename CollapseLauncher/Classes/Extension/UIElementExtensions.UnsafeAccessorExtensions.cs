using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Runtime.CompilerServices;

#nullable enable
namespace CollapseLauncher.Extension
{
    internal static partial class UIElementExtensions
    {
        /// <summary>
        /// Set the cursor for the element.
        /// </summary>
        /// <param name="element">The <seealso cref="UIElement"/> member of an element</param>
        /// <param name="inputCursor">The cursor you want to set. Use <see cref="InputSystemCursor.Create"/> to choose the cursor you want to set.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ProtectedCursor")]
        internal static extern void SetCursor(this UIElement element, InputCursor inputCursor);

        /// <summary>
        /// Set the cursor for the element.
        /// </summary>
        /// <param name="element">The <seealso cref="UIElement"/> member of an element</param>
        /// <param name="inputCursor">The cursor you want to set. Use <see cref="InputSystemCursor.Create"/> to choose the cursor you want to set.</param>
        internal static ref T WithCursor<T>(this T element, InputCursor inputCursor) where T : UIElement
        {
            element.SetCursor(inputCursor);
            return ref Unsafe.AsRef(ref element);
        }
    }
}
