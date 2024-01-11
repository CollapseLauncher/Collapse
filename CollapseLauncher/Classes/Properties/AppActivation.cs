using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public static class AppActivation
    {
        public static void Enable()
        {
            AppInstance.GetCurrent().Activated += App_Activated;
        }

        private static void App_Activated(object sender, AppActivationArguments e)
        {
            if (IsMultipleInstanceEnabled)
                return;
            if (e.Kind != ExtendedActivationKind.Launch)
                return;
            if (e.Data == null)
                return;

            var args = e.Data as ILaunchActivatedEventArgs;
            ArgumentParser.ResetRootCommand();
            m_arguments = new Arguments();

            // Matches anything that is between two \" or " and anything that is not a space.
            var splitArgs = Regex.Matches(args.Arguments, @"[\""].+?[\""]|[^ ]+")
                                    .Cast<Match>()
                                    .Select(x => x.Value.Trim('"'));

            ArgumentParser.ParseArguments(splitArgs.Skip(1).ToArray());

            if (m_arguments.StartGame != null)
            {
                m_mainPage?.OpenAppActivation();
                return;
            }
        }

        public static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey(m_appMode.ToString());

            if (!keyInstance.IsCurrent && !IsMultipleInstanceEnabled)
            {
                isRedirect = true;
                keyInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
            }
            return isRedirect;
        }

        public static void Disable()
        {
            AppInstance.GetCurrent().Activated -= App_Activated;
        }
    }
}
