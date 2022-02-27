using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Animation;

namespace CollapseLauncher
{
    #region ErrorSenderRegion
    public enum ErrorType { Unhandled, GameError }

    internal static class ErrorSender
    {
        static ErrorSenderInvoker invoker = new ErrorSenderInvoker();
        public static string ExceptionContent;
        public static string ExceptionTitle;
        public static string ExceptionSubtitle;
        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled) => invoker.SendException(e, eT);
    }

    internal class ErrorSenderInvoker
    {
        public static event EventHandler<ErrorProperties> ExceptionEvent;
        public void SendException(Exception e, ErrorType eT) => ExceptionEvent?.Invoke(this, new ErrorProperties(e, eT));
    }

    internal class ErrorProperties
    {
        internal ErrorProperties(Exception e, ErrorType errorType)
        {
            Exception = e;
            ExceptionString = e.ToString();
            ErrorSender.ExceptionContent = ExceptionString;

            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ErrorSender.ExceptionTitle = "Unhandled Error";
                    ErrorSender.ExceptionSubtitle = "An Unhandled Error has occured with an Exception Throw below:";
                    break;
                case ErrorType.GameError:
                    ErrorSender.ExceptionTitle = "Game Crashed";
                    ErrorSender.ExceptionSubtitle = "The game has crashed with error details below:";
                    break;
            }
        }
        public Exception Exception { get; private set; }
        public string ExceptionString { get; private set; }
    }
    #endregion

    #region MainFrameRegion
    internal static class MainFrameChanger
    {
        static MainFrameChangerInvoker invoker = new MainFrameChangerInvoker();
        public static void ChangeMainFrame(Type e) => invoker.ChangeMainFrame(e, new DrillInNavigationTransitionInfo());
        public static void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => invoker.ChangeMainFrame(e, eT);
    }

    internal class MainFrameChangerInvoker
    {
        public static event EventHandler<MainFrameProperties> FrameEvent;
        public void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => FrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
    }

    internal class MainFrameProperties
    {
        internal MainFrameProperties(Type FrameTo, NavigationTransitionInfo Transition)
        {
            this.FrameTo = FrameTo;
            this.Transition = Transition;
        }
        public Type FrameTo { get; private set; }
        public NavigationTransitionInfo Transition { get; private set; }
    }
    #endregion
}
