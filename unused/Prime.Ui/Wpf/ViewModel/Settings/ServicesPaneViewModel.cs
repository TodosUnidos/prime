﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Prime.Common;
using Prime.Common.Wallet;
using Prime.Utility;

namespace Prime.Ui.Wpf.ViewModel
{
    public class ServicesPaneViewModel : DocumentPaneViewModel
    {
        public ServicesPaneViewModel()
        {
            Populate();
        }

        public ObservableCollection<ServiceLineItem> ServicesObservable { get; private set; } = new ObservableCollection<ServiceLineItem>();

        private bool _hasApiKey = true;
        public bool HasApiKey
        {
            get => _hasApiKey;
            set => SetAfter(ref _hasApiKey, value, (v) => Populate());
        }

        private void Populate()
        {
            UiDispatcher.Invoke(() =>
            {
                ServicesObservable.Clear();
                var q = Networks.I.Providers.AsEnumerable();

                if (HasApiKey)
                    q = q.FilterType<INetworkProvider, INetworkProviderPrivate>();

                q = q.Where(x => x.IsDirect);

                foreach (var i in q.OrderBy(x => x.Network.Name))
                    ServicesObservable.Add(new ServiceLineItem(i));
            });
        }

        public override CommandContent GetPageCommand()
        {
            return new SimpleContentCommand("services");
        }
    }
}
