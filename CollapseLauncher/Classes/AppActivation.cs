using Microsoft.Windows.AppLifecycle;
using System.Linq;
using System;
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
            ArgumentParser.ParseArguments(args.Arguments.Split(" ").Skip(2).ToArray());

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
