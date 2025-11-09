using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Loading
{
    internal static class LoadingMessageHelper
    {
        private static          MainWindow               _currentMainWindow;
        private static          bool                     _isLoadingProgressIndeterminate = true;
        private static          bool                     _isCurrentlyShow;
        private static          DispatcherQueue          _currentDispatcherQueue;
        private static readonly List<RoutedEventHandler> CurrentActionButtonHandler = [];

        /// <summary>
        /// Initialize the necessary <c>MainWindow</c> for spawning the loading frame overlay.
        /// </summary>
        internal static void Initialize()
        {
            if (WindowUtility.CurrentWindow is MainWindow window)
                _currentMainWindow = window;

            _currentDispatcherQueue = _currentMainWindow.DispatcherQueue;
        }

        /// <summary>
        /// Set the message of the loading frame.
        /// </summary>
        /// <param name="title">Set the title of the loading frame. You can set the value to <c>null</c> to ignore the change of the title or <c>string.Empty</c> to disable the title</param>
        /// <param name="subtitle">Set the subtitle of the loading frame. You can set the value to <c>null</c> to ignore the change of the subtitle or <c>string.Empty</c> to disable the subtitle</param>
        internal static void SetMessage(string title, string subtitle)
        {
            _currentDispatcherQueue.TryEnqueue(() =>
            {
                if (title != null) _currentMainWindow.LoadingStatusTextTitle.Text = title;
                if (subtitle != null) _currentMainWindow.LoadingStatusTextSubtitle.Text = subtitle;

                _currentMainWindow.LoadingStatusTextSeparator.Visibility = title == string.Empty || subtitle == string.Empty ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        /// <summary>
        /// Set the value of the progress ring.
        /// <br/><br/>
        /// Note: If you would like to set the progress ring's value, make sure to set the <c>isProgressIndeterminate</c> to <c>true</c> with <c>SetProgressBarState()</c> method.
        /// </summary>
        /// <param name="value">Set the current value of the progress ring</param>
        internal static void SetProgressBarValue(double value) => _currentDispatcherQueue.TryEnqueue(() => _currentMainWindow.LoadingStatusProgressRing.Value = value);

        /// <summary>
        /// Set the state and the maximum value of the progress ring.
        /// </summary>
        /// <param name="maxValue">Set the maximum value of the progress ring.</param>
        /// <param name="isProgressIndeterminate">Set the state of the progress ring's indeterminate.</param>
        internal static void SetProgressBarState(double maxValue = 100d, bool isProgressIndeterminate = true)
        {
            _currentDispatcherQueue.TryEnqueue(() =>
            {
                _isLoadingProgressIndeterminate = isProgressIndeterminate;
                _currentMainWindow.LoadingStatusProgressRing.Maximum = maxValue;
                _currentMainWindow.LoadingStatusProgressRing.IsIndeterminate = _isLoadingProgressIndeterminate;
            });
        }

        /// <summary>
        /// Show the loading frame.
        /// </summary>
        internal static async void ShowLoadingFrame()
        {
            try
            {
                if (_currentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.HasThreadAccessSafe())
                {
                    await ShowLoadingFrameInner();
                    return;
                }

                await _currentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.EnqueueAsync(ShowLoadingFrameInner);
            }
            catch
            {
                // ignored
            }
        }

        private static Task ShowLoadingFrameInner()
        {
            if (_isCurrentlyShow) return Task.CompletedTask;

            _isCurrentlyShow                                            = true;
            _currentMainWindow.LoadingStatusGrid.Visibility           = Visibility.Visible;
            _currentMainWindow.LoadingStatusBackgroundGrid.Visibility = Visibility.Visible;

            TimeSpan duration = TimeSpan.FromSeconds(0.25);

            return Task.WhenAll(_currentMainWindow.LoadingStatusBackgroundGrid.StartAnimation(duration,
                                                                                            _currentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                               _currentMainWindow.LoadingStatusGrid.StartAnimation(duration,
                                                                                  _currentMainWindow.LoadingStatusGrid.GetElementCompositor().CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, _currentMainWindow.LoadingStatusGrid.Translation.Z), new Vector3(0, (float)(_currentMainWindow.LoadingStatusGrid.ActualHeight + 16), _currentMainWindow.LoadingStatusGrid.Translation.Z)),
                                                                                  _currentMainWindow.LoadingStatusGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0))
                              );
        }

        /// <summary>
        /// Hide the loading frame (also hide the action button).
        /// </summary>
        internal static async void HideLoadingFrame()
        {
            try
            {
                if (_currentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.HasThreadAccessSafe())
                {
                    await HideLoadingFrameInner();
                    return;
                }

                await _currentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.EnqueueAsync(HideLoadingFrameInner);
            }
            catch
            {
                // ignored
            }
        }

        private static Task HideLoadingFrameInner()
        {
            if (!_isCurrentlyShow) return Task.CompletedTask;

            _isCurrentlyShow = false;

            TimeSpan duration = TimeSpan.FromSeconds(0.25);
            return Task.WhenAll(_currentMainWindow.LoadingStatusBackgroundGrid.StartAnimation(duration,
                                    _currentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1)),
                                _currentMainWindow.LoadingStatusGrid.StartAnimation(duration,
                                    _currentMainWindow.LoadingStatusGrid.GetElementCompositor().CreateVector3KeyFrameAnimation("Translation", new Vector3(0, (float)(_currentMainWindow.LoadingStatusGrid.ActualHeight + 16), _currentMainWindow.LoadingStatusGrid.Translation.Z), new Vector3(0, 0, _currentMainWindow.LoadingStatusGrid.Translation.Z)),
                                    _currentMainWindow.LoadingStatusGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1))
                               ).ContinueWith((_, _) =>
                                              {
                                                  _currentMainWindow.DispatcherQueue.TryEnqueue(() =>
                                                  {
                                                      _currentMainWindow.LoadingStatusGrid.Visibility = Visibility.Collapsed;
                                                      _currentMainWindow.LoadingStatusBackgroundGrid.Visibility = Visibility.Collapsed;
                                                      HideActionButton();
                                                  });
                                              }, null);
        }

        /// <summary>
        /// Show the action button for the loading frame.
        /// </summary>
        /// <param name="buttonContent">Set the content of the button. <c>string</c> or <c>FrameworkElement</c> (UI element) is acceptable.</param>
        /// <param name="buttonIconGlyph">
        /// Set the glyph icon of the button. Set it to <c>null</c> or <c>string.Empty</c> to disable the icon.
        /// <br/><br/>
        /// Note: You can directly use the unicode character as an input or write it with "\u" escape (for example: "\uf00d" for X mark icon).
        /// <br/>Keep in mind that you can only use the glyph that's available in "Font Awesome Solid" font.<br/>
        /// </param>
        /// <param name="routedEvent">Set the event callback for the button's click event.</param>
        internal static void ShowActionButton(object buttonContent, string buttonIconGlyph = null, RoutedEventHandler routedEvent = null)
        {
            _currentDispatcherQueue.TryEnqueue(() =>
            {
                if (routedEvent != null)
                {
                    CurrentActionButtonHandler!.Add(routedEvent);
                    _currentMainWindow.LoadingStatusActionButton.Click += routedEvent;
                }

                bool isHasIcon = !string.IsNullOrEmpty(buttonIconGlyph);
                _currentMainWindow.LoadingStatusActionButtonIcon.Visibility = isHasIcon ? Visibility.Visible : Visibility.Collapsed;
                _currentMainWindow.LoadingStatusActionButton.Visibility = Visibility.Visible;
                if (isHasIcon) _currentMainWindow.LoadingStatusActionButtonIcon.Glyph = buttonIconGlyph;

                if (buttonContent is string buttonText)
                {
                    TextBlock textBlock = new()
                    {
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, isHasIcon ? -2 : 0, 0, 0)
                    };
                    textBlock.AddTextBlockLine(buttonText, FontWeights.SemiBold);
                    _currentMainWindow.LoadingStatusActionButtonContentContainer.Children.Clear();
                    _currentMainWindow.LoadingStatusActionButtonContentContainer.AddElementToGridRowColumn(textBlock);
                    return;
                }

                _currentMainWindow.LoadingStatusActionButtonContentContainer.Children.Clear();
                _currentMainWindow.LoadingStatusActionButtonContentContainer.AddElementToGridRowColumn(buttonContent as FrameworkElement);
            });
        }

        /// <summary>
        /// Hide the action button from the loading frame.
        /// </summary>
        internal static void HideActionButton()
        {
            _currentDispatcherQueue.TryEnqueue(() =>
            {
                if (CurrentActionButtonHandler.Count > 0)
                {
                    foreach (var t in CurrentActionButtonHandler)
                        _currentMainWindow.LoadingStatusActionButton.Click -= t;

                    CurrentActionButtonHandler.Clear();
                }

                _currentMainWindow.LoadingStatusActionButton.Visibility = Visibility.Collapsed;
            });
        }
    }
}
