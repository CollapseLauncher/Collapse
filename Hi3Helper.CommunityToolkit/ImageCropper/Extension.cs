namespace Hi3Helper.CommunityToolkit.WinUI.Controls
{
    internal static class Extension
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToRect(this Size size)
        {
            return new Rect(0, 0, size.Width, size.Height);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToRect(this Point point, double width, double height)
        {
            return new Rect(point.X, point.Y, width, height);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToRect(this Point point, Point end)
        {
            return new Rect(point, end);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToRect(this Point point, Size size)
        {
            return new Rect(point, size);
        }
    }
}
