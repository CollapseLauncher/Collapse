using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Linq;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Extension
{
    public class TextBlockExtension
    {
        public static readonly DependencyProperty RemoveEmptyRunsProperty =
            DependencyProperty.RegisterAttached("RemoveEmptyRuns", typeof(bool),
                                                typeof(TextBlock), new PropertyMetadata(false));

        public static readonly DependencyProperty PreserveSpaceProperty =
            DependencyProperty.RegisterAttached("PreserveSpace", typeof(bool),
                                                typeof(Run), new PropertyMetadata(false));

        public static bool GetRemoveEmptyRuns(DependencyObject obj)
        {
            return (bool)obj.GetValue(RemoveEmptyRunsProperty);
        }

        public static void SetRemoveEmptyRuns(DependencyObject obj, bool value)
        {
            obj.SetValue(RemoveEmptyRunsProperty, value);

            if (!value)
            {
                return;
            }

            if (obj is TextBlock tb)
            {
                tb.Loaded += Tb_Loaded;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static bool GetPreserveSpace(DependencyObject obj)
        {
            return (bool)obj.GetValue(PreserveSpaceProperty);
        }

        public static void SetPreserveSpace(DependencyObject obj, bool value)
        {
            obj.SetValue(PreserveSpaceProperty, value);
        }

        private static void Tb_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBlock tb)
            {
                return;
            }

            tb.Loaded -= Tb_Loaded;

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (Run run in tb.Inlines
                                  .OfType<Run>()
                                  .Where(x => string.IsNullOrWhiteSpace(x.Text))
                                  .ToList())
            {
                bool isExcept = GetPreserveSpace(run);
                if (isExcept)
                {
                    continue;
                }
                tb.Inlines.Remove(run);
            }
        }
    }
}
