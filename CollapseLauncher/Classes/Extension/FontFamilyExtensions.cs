using Microsoft.UI.Xaml.Media;

namespace CollapseLauncher.Extension
{
    internal static class FontCollections
    {
        private static FontFamily cached_FontAwesomeSolid;
        private static FontFamily cached_FontAwesomeRegular;
        private static FontFamily cached_FontAwesomeBrand;

        internal static FontFamily FontAwesomeSolid
        {
            get
            {
                if (cached_FontAwesomeSolid == null)
                    cached_FontAwesomeSolid = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesomeSolid");

                return cached_FontAwesomeSolid;
            }
            set => UIElementExtensions.SetApplicationResource("FontAwesomeSolid", value);
        }

        internal static FontFamily FontAwesomeRegular
        {
            get
            {
                if (cached_FontAwesomeRegular == null)
                    cached_FontAwesomeRegular = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesome");

                return cached_FontAwesomeRegular;
            }
            set => UIElementExtensions.SetApplicationResource("FontAwesome", value);
        }

        internal static FontFamily FontAwesomeBrand
        {
            get
            {
                if (cached_FontAwesomeBrand == null)
                    cached_FontAwesomeBrand = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesomeBrand");

                return cached_FontAwesomeBrand;
            }
            set => UIElementExtensions.SetApplicationResource("FontAwesomeBrand", value);
        }
    }
}
