﻿using Crystal3;
using Crystal3.Navigation;
using Neptunium.Data;
using Neptunium.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Neptunium.Shared;
using Windows.Media.Playback;
using System.Diagnostics;
using Neptunium.Media;
using Neptunium.Logging;
using System.Threading.Tasks;
using Windows.ApplicationModel.Email;
using Neptunium.Managers;
using Windows.Storage;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.System.RemoteSystems;
using Kukkii;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=402347&clcid=0x409

namespace Neptunium
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : CrystalApplication
    {
        public static BackgroundAccessStatus BackgroundAccess { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
#if DEBUG
            Application.Current.UnhandledException += Current_UnhandledException;
#endif

            //For Xbox One
            this.RequiresPointerMode = Windows.UI.Xaml.ApplicationRequiresPointerMode.WhenRequested;

            CoreInit();
        }

        protected override void OnConfigure()
        {
            base.OnConfigure();

            this.Options.HandleSystemBackNavigation = CrystalApplication.GetDevicePlatform() != Crystal3.Core.Platform.Xbox;
        }

        private static async void CoreInit()
        {
            CookieJar.ApplicationName = "Neptunium";

            if ((BackgroundAccess = BackgroundExecutionManager.GetAccessStatus()) == BackgroundAccessStatus.Unspecified)
                BackgroundAccess = await BackgroundExecutionManager.RequestAccessAsync();

            //initialize app settings
            //todo add all settings

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(AppSettings.ShowSongNotifications))
#if RELEASE
                ApplicationData.Current.LocalSettings.Values.Add(AppSettings.ShowSongNotifications, false);
#else
                ApplicationData.Current.LocalSettings.Values.Add(AppSettings.ShowSongNotifications, true);
#endif

            await LogManager.InitializeAsync();
            await StationMediaPlayer.InitializeAsync();

            Hqub.MusicBrainz.API.MyHttpClient.UserAgent = "Neptunium/0.1 ( amrykid@gmail.com )";

            LogManager.Info(typeof(App), "CoreInitialization Complete");
        }

        private async void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            LogManager.Log(typeof(App), "BEGIN Unhandled Exception");
            LogManager.Error(typeof(App), e.Exception.ToString());
            LogManager.Error(typeof(App), e.Exception.StackTrace);

            if (e.Exception.InnerException != null)
                LogManager.Error(typeof(App), e.Exception.InnerException.ToString());

            LogManager.Error(typeof(App), e.Message);
            LogManager.Log(typeof(App), "END Unhandled Exception");

            await Task.Delay(50);

            //Application.Current.Exit();
        }

        private void PostUIInit()
        {
#if RELEASE
            if (CrystalApplication.GetDevicePlatform() == Crystal3.Core.Platform.Mobile)
#endif
            if (!CarModeManager.IsInitialized)
                CarModeManager.Initialize();

            if (!ContinuedAppExperienceManager.IsInitialized)
                ContinuedAppExperienceManager.InitializeAsync();

            //Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            if (CrystalApplication.GetDevicePlatform() == Crystal3.Core.Platform.Xbox)
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView()
                    .SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
        }

        public override async Task OnFreshLaunchAsync(LaunchActivatedEventArgs args)
        {
            PostUIInit();

            LogManager.Info(typeof(App), "Application Launching");
            WindowManager.GetNavigationManagerForCurrentWindow()
                .RootNavigationService.NavigateTo<AppShellViewModel>();

            await Task.CompletedTask;
        }

        public override async Task OnActivationAsync(IActivatedEventArgs args)
        {

            if (!WindowManager.GetNavigationManagerForCurrentWindow()
                .RootNavigationService.IsNavigatedTo<AppShellViewModel>())
            {
                PostUIInit();

                WindowManager.GetNavigationManagerForCurrentWindow()
                    .RootNavigationService.NavigateTo<AppShellViewModel>();
            }

            if (args.Kind == ActivationKind.Protocol)
            {
                var pargs = args as ProtocolActivatedEventArgs;

                var uri = pargs.Uri;
                switch(uri.LocalPath.ToLower())
                {
                    case "play-station":
                        {
                            try
                            {
                                var query = uri.Query
                                    .Substring(1)
                                    .Split('&')
                                    .Select(x =>
                                        new KeyValuePair<string, string>(
                                            x.Split('=')[0],
                                            x.Split('=')[1])); //remote the "?"

                                var stationName = Uri.EscapeUriString(query.First(x => x.Key.ToLower() == "station").Value);

                                if (!StationDataManager.IsInitialized)
                                    await StationDataManager.InitializeAsync();

                                var station = StationDataManager.Stations.First(x => x.Name == stationName);

                                await StationMediaPlayer.PlayStationAsync(station);
                            }
                            catch (Exception)
                            {

                            }
                        }
                        break;
                }
            }

            await Task.CompletedTask;
        }

        public override async Task OnSuspendingAsync()
        {
            LogManager.Info(typeof(App), "Application Suspending");

            ContinuedAppExperienceManager.StopWatchingForRemoteSystems();

            await base.OnSuspendingAsync();
        }

        public override async Task OnResumingAsync()
        {
            //await LogManager.InitializeAsync();

            LogManager.Info(typeof(App), "Application Resuming");

            if (ContinuedAppExperienceManager.RemoteSystemAccess == RemoteSystemAccessStatus.Unspecified)
                await ContinuedAppExperienceManager.InitializeAsync();
            ContinuedAppExperienceManager.StartWatchingForRemoteSystems();

            await base.OnResumingAsync();
        }

        public override Task OnBackgroundActivatedAsync(BackgroundActivatedEventArgs args)
        {
            switch (args.TaskInstance.Task.Name)
            {
                case ContinuedAppExperienceManager.ContinuedAppExperienceAppServiceName:
                    if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails)
                    {
                        ContinuedAppExperienceManager.HandleBackgroundActivation(args.TaskInstance.TriggerDetails as AppServiceTriggerDetails);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
