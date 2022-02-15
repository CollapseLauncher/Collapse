using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    public enum ErrorType
    {
        Unhandled,
        GameError
    }
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

    public class ErrorProperties
    {
        public ErrorProperties(Exception e, ErrorType errorType)
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
}
