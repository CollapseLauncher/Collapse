using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Runtime.CompilerServices;
using WinRT;

#nullable enable
namespace CollapseLauncher.Extension
{
    public static partial class UIElementExtensions
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
        internal static T WithCursor<T>(this T element, InputCursor inputCursor) where T : UIElement
        {
            element.SetCursor(inputCursor);
            return element;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_disposedFlags")]
        private static extern ref int GetObjectReferenceDisposeFlags(this IObjectReference obj);

        /// <summary>
        /// Check whether a WinRT object has been disposed.
        /// </summary>
        /// <returns><see langword="true"/> if object is already disposed. Otherwise, <see langword="false"/>.</returns>
        internal static bool IsObjectDisposed(this IWinRTObject? winRtObject)
        {
            if (winRtObject == null)
            {
                return true;
            }

            IObjectReference reference = winRtObject.NativeObject;
            ref int disposeFlags = ref reference.GetObjectReferenceDisposeFlags();

            return disposeFlags > 0;
        }
    }
}
