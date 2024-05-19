using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CollapseLauncher.Extension
{
    internal static class IconElementExtensions
    {
        internal static void ApplyShadow(this IconElement iconElement)
        {
            switch (iconElement)
            {
                case FontIcon:
                {
                    var shadowGrid = (Grid)iconElement.FindDescendant("ShadowGrid");
                    if (shadowGrid != null) return;
                    shadowGrid = new Grid { Name = "ShadowGrid" };

                    var rootGrid = iconElement.FindDescendant<Grid>();
                    if (rootGrid != null)
                    {
                        rootGrid.Children.Add(shadowGrid);
                        Canvas.SetZIndex(shadowGrid, -1);
                    }
                    iconElement.Loaded += (_, _) =>
                    {
                        if (rootGrid == null)
                        {
                            rootGrid = iconElement.FindDescendant<Grid>();
                            if (rootGrid == null)
                                throw new NullReferenceException("No avaliable element");
                            rootGrid.Children.Add(shadowGrid);
                            Canvas.SetZIndex(shadowGrid, -1);
                        }
                    };

                    ApplyShadow(iconElement, shadowGrid);

                    break;
                }
                case AnimatedIcon animatedIcon:
                {
                    Grid shadowGrid = null;
                    switch (iconElement.Parent)
                    {
                        case Grid grid:
                        {
                            var rootGrid = grid;
                            shadowGrid = (Grid)rootGrid.FindDescendant("ShadowGrid");
                            if (shadowGrid != null) return;
                            shadowGrid = new Grid { Name = "ShadowGrid" };
                            rootGrid.Children.Add(shadowGrid);
                            Canvas.SetZIndex(shadowGrid, -1);
                            break;
                        }
                        case Border border:
                        {
                            var rootGrid = border.Child as Grid;
                            if (rootGrid != null && rootGrid.Name == "RootGrid") return;
                            rootGrid     = new Grid { Name = "RootGrid" };
                            border.Child = rootGrid;
                            shadowGrid   = new Grid { Name = "ShadowGrid" };
                            rootGrid.Children.Add(shadowGrid);
                            rootGrid.Children.Add(animatedIcon);
                            break;
                        }
                    }

                    if (shadowGrid == null) return;
                    ApplyShadow(iconElement, shadowGrid);
                    break;
                }
            }
        }

        private static void ApplyShadow(FrameworkElement from, FrameworkElement to)
        {
            var shadow = new AttachedDropShadow()
            {
                Color      = Colors.Gray,
                BlurRadius = 20,
                Opacity    = 0.25,
                CastTo     = to,
            };
            Effects.SetShadow(from, shadow);
        }
    }
}
