﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Prime.Common;
using Framework.UI;
using Hardcodet.Wpf.TaskbarNotification;
using Prime.Utility;
using Prime.Ui.Wpf;
using Prime.Ui.Wpf.ViewModel;

namespace prime
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private TaskbarIcon notifyIcon;
        private ICore _prime;

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            GlobalMisc.I.MainAssembly = Assembly.GetExecutingAssembly();

            _prime = TypeCatalogue.I.ImplementInstancesI<ICore>().FirstOrDefault();
            _prime.Start(); //INIT PRIME //THIS IS A HACK FOR NOW

            PrimeWpf.I.SetDispatcher();

            this.Startup += App_Startup;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            UserContext.Current.WindowManager.Init(Dispatcher);
        }
        
        
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logging.I.DefaultLogger.Fatal("App Exception: " + e.Exception.Message);
            e.Handled = true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //create the notifyicon (it's a resource declared in NotifyIconResources.xaml
            notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            notifyIcon.Dispose(); //the icon would clean up automatically, but this is cleaner
            base.OnExit(e);
        }
    }
}
