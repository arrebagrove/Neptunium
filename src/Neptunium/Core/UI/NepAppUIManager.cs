﻿using Crystal3.Navigation;
using Crystal3.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using static Neptunium.NepApp;

namespace Neptunium.Core.UI
{
    public class NepAppUIManager : INotifyPropertyChanged, INepAppFunctionManager
    {
        /// <summary>
        /// From INotifyPropertyChanged
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #region Core
        private NavigationServiceBase inlineNavigationService = null;
        private string _viewTitle = "PAGE TITLE";
        private ObservableCollection<NepAppUINavigationItem> navigationItems = null;
        private WindowService windowService = null;
        internal NepAppUIManager()
        {
            navigationItems = new ObservableCollection<NepAppUINavigationItem>();
            NavigationItems = new ReadOnlyObservableCollection<NepAppUINavigationItem>(navigationItems);
            Notifier = new NepAppUIManagerNotifier();
            windowService = WindowManager.GetWindowServiceForCurrentWindow();
        }

        internal void SetNavigationService(NavigationServiceBase navService)
        {
            if (navService == null) throw new ArgumentNullException(nameof(navService));

            if (inlineNavigationService != null)
            {
                //unsubscribe from the previous nav service
                ((FrameNavigationService)inlineNavigationService).Navigated -= NepAppUIManager_Navigated;
            }

            inlineNavigationService = navService;
            ((FrameNavigationService)inlineNavigationService).Navigated += NepAppUIManager_Navigated;
        }

        private void NepAppUIManager_Navigated(object sender, CrystalNavigationEventArgs e)
        {
            UpdateSelectedNavigationItems();

            windowService.SetAppViewBackButtonVisibility(inlineNavigationService.CanGoBackward);
        }

        internal void SetOverlayParentAndSnackBarContainer(Grid parentControl, Grid snackBarContainer)
        {
            if (parentControl == null) throw new ArgumentNullException(nameof(parentControl));
            if (snackBarContainer == null) throw new ArgumentNullException(nameof(snackBarContainer));

            Overlay = new NepAppUIManagerDialogCoordinator(this, parentControl, snackBarContainer);
        }

        private void UpdateSelectedNavigationItems()
        {
            var pageType = ((FrameNavigationService)inlineNavigationService).NavigationFrame.CurrentSourcePageType;

            foreach (NepAppUINavigationItem navItem in navigationItems)
            {
                navItem.IsSelected = false;
            }

            NepAppUINavigationItem item = null;
            item = navigationItems.FirstOrDefault(x =>
            {
                var navAttr = pageType.GetTypeInfo().GetCustomAttribute<Crystal3.Navigation.NavigationViewModelAttribute>();
                return navAttr.ViewModel == x.NavigationViewModelType;
            });

            if (item != null)
            {
                item.IsSelected = true;
                ViewTitle = item.DisplayText;
            }
            else
            {
                ViewTitle = "";
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentNullException("propertyName");

            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public string ViewTitle { get { return _viewTitle.ToUpper(); } private set { _viewTitle = value; RaisePropertyChanged(nameof(ViewTitle)); } }
        public ReadOnlyObservableCollection<NepAppUINavigationItem> NavigationItems { get; private set; }
        public NepAppUIManagerNotifier Notifier { get; private set; }
        public NepAppUIManagerDialogCoordinator Overlay { get; private set; }
        public async Task ShowErrorDialogAsync(string title, string message)
        {
            await await App.Dispatcher.RunWhenIdleAsync(() =>
            {
                MessageDialog dialog = new MessageDialog(message);
                dialog.Title = title;
                return dialog.ShowAsync();
            });
        }
        #endregion

        public void AddNavigationRoute(string displayText, Type navigationViewModel, string symbol = "")
        {
            NepAppUINavigationItem navItem = new NepAppUINavigationItem();
            navItem.DisplayText = displayText;
            navItem.Symbol = symbol;
            navItem.NavigationViewModelType = navigationViewModel;
            navItem.Command = new RelayCommand(x =>
            {
                NavigateToItem(navItem, x);
            });
            navigationItems.Add(navItem);
        }

        public void NavigateToItem(NepAppUINavigationItem navItem, object parameter = null)
        {
            if (inlineNavigationService == null)
                throw new InvalidOperationException(nameof(inlineNavigationService) + " is null.");

            if (!((FrameNavigationService)inlineNavigationService).IsNavigatedTo(navItem.NavigationViewModelType))
                ((FrameNavigationService)inlineNavigationService).Navigate(navItem.NavigationViewModelType, parameter);

            //UpdateSelectedNavigationItems();

            ViewTitle = navItem.DisplayText;
        }
    }
}
