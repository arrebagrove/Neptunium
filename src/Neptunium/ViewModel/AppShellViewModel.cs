﻿using Crystal3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crystal3.Navigation;

namespace Neptunium.ViewModel
{
    public class AppShellViewModel: ViewModelBase
    {
        public AppShellViewModel()
        {
            NepApp.UI.AddNavigationRoute("Stations", typeof(StationsPageViewModel), "");
        }

        protected override void OnNavigatedTo(object sender, CrystalNavigationEventArgs e)
        {
            base.OnNavigatedTo(sender, e);

            
        }
    }
}
