using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Definitions.Series;
using LiveCharts.Wpf;
using NodaTime;
using Prime.Common;
using Prime.Core;
using Prime.Utility;

namespace Prime.Ui.Wpf.ViewModel
{
    public class PriceChartPaneViewModel : DocumentPaneViewModel, IPaneProvider
    {
        private OhlcDataAdapter _adapter;
        public ChartGroupViewModel ChartGroupViewModel { get; private set; }
        private readonly AssetPair _pair;
        private readonly DebouncerDispatched _debouncer;
        private ChartViewModel _volumeChart;
        private ChartViewModel _priceChart;
        private Timer _liveTimer;
        private readonly object _lock = new object();
        private CoverageMapMemory _renderedCoverage = new CoverageMapMemory();

        private readonly List<ZoomViewModel> _chartZooms = new List<ZoomViewModel>();
        private readonly List<ZoomViewModel> _allZooms = new List<ZoomViewModel>();
        public readonly OverviewZoomViewModel OverviewZoom;
        public readonly ReceiverZoomViewModel ReceiverZoom;
        private ResolutionSourceProvider _chartResolutionProvider;

        public readonly TimeResolution OverviewDefaultResolution = TimeResolution.Day;
        public readonly TimeResolution ReceiverDefaultResolution = TimeResolution.Day;

        public EventHandler OnDataUpdate;

        public EventHandler OnRangeChange;

        public PriceChartPaneViewModel() { }

        private PriceChartPaneViewModel(AssetPair pair)
        {
            _debouncer = new DebouncerDispatched(UiDispatcher);
            _pair = pair;

            Key = _pair.ToString();
            Title = _pair.ToString();
            CanClose = true;
            IsActive = true;
            IsSelected = true;

            OverviewZoom = new OverviewZoomViewModel(OverviewDefaultResolution);
            ReceiverZoom = new ReceiverZoomViewModel(ReceiverDefaultResolution);
            
            M.RegisterAsync<AssetPairDiscoveryResultMessage>(this, AssetPairDiscoveryResultMessage);
            M.SendAsync(new AssetPairDiscoveryRequestMessage(_pair));

            SetDataStatus("Provider Discovery", true);
        }

        public bool IsFor(CommandBase command)
        {
            return command is AssetGoCommand;
        }

        public DocumentPaneViewModel GetInstance(ScreenViewModel model, CommandBase command)
        {
            var c = command as AssetGoCommand;
            var pair = new AssetPair(c.Asset, UserContext.Current.QuoteAsset);
            return new PriceChartPaneViewModel(pair);
        }

        private void AssetPairDiscoveryResultMessage(AssetPairDiscoveryResultMessage m)
        {
            if (!Equals(m.RequestRequestMessage.Pair, _pair) || m.RequestRequestMessage.Network != null)
                return;

            M.UnregisterD(this);

            if (m.IsFailed)
            {
                SetDataStatus("No providers found", false);
                return;
            }

            SetDataStatus("Initialising", true);

            var ctx = new OhlcResolutionContext()
            {
                AssetPairProviders = m.DiscoverFirst,
                Pair = _pair,
                RequestFullDaily = true,
                StatusEntry = (s) => UiDispatcher.Invoke(() => DataStatus = s)
            };

            _adapter = new OhlcDataAdapter(ctx);

            _allZooms.Add(OverviewZoom);

            ChartGroupViewModel = new ChartGroupViewModel(this, OverviewZoom)
            {
                ResolutionSelected = ReceiverDefaultResolution
            };

            QueueWork(InitDataThread);
        }

        public bool AllowLive { get; set; } = true;

        private string _dataStatus;
        public string DataStatus
        {
            get => _dataStatus;
            set => Set(ref _dataStatus, value);
        }

        public bool IsDataBusy
        {
            get => _isDataBusy;
            set => Set(ref _isDataBusy, value);
        }

        public bool IsGraphReady
        {
            get => _isGraphReady;
            set => SetAfter(ref _isGraphReady, value, a=> ChartGroupViewModel.InvalidateRangeProperties());
        }


        private void InitDataThread()
        {
            SetDataStatus("Discovering Data");
            _adapter.Init();
            SetDataStatus();

            var range = new TimeRange(-ReceiverDefaultResolution.GetDefaultTimeSpan(), ReceiverDefaultResolution).RemoveLiveRange();

            // Get the data for the chart from the datasource

            var priceData = RequestData(range);
            if (priceData.IsEmpty())
                return;

            SetDataStatus("Initialising chart");

            UiDispatcher.Invoke(delegate
            {
                CreateCharts(priceData);
                SetupZoomEvents();
                SetDataStatus();
            });

            ChartGroupViewModel.PropertyChanged += OnChartGroupViewModelOnPropertyChanged;

            InitLiveTimer();
        }

        private void OnChartGroupViewModelOnPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ChartGroupViewModel.ResolutionSelected))
                QueueWork(UpdateFromResolutionChange);
        }

        private DateTime _lastLiveDataUpdate = DateTime.MinValue;

        private void LiveUpdateElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                if (!AllowLive)
                    return;

                var datastale = _lastLiveDataUpdate.IsBeforeTheLast(TimeSpan.FromSeconds(10));

                _chartZooms.FirstOrDefault()?.Update(datastale);

                if (!datastale)
                    return;

                UpdateData(true);
                _lastLiveDataUpdate = DateTime.UtcNow;
            }
        }

        private void SetupZoomEvents()
        {
            foreach (var zoom in _allZooms)
                zoom.OnRangePreviewChange += OnZoomOnRangePreviewChange;
        }

        private void OnZoomOnRangePreviewChange(object s, EventArgs e)
        {
            _debouncer.Debounce(25, _ =>
            {
                OnRangeChange?.Invoke(this, EventArgs.Empty);
                QueueWork(() => UpdateData());
            });

            var sender = s as ZoomViewModel;
            foreach (var oz in _allZooms)
            {
                if (oz == sender)
                    continue;

                oz.SuspendRangeEventTill = DateTime.UtcNow.AddMilliseconds(200);
                oz.ZoomToRange(sender.GetTimeRange(oz.Resolution));
            }
        }

        private void CreateCharts(OhlcData sourceData)
        {
            if (!sourceData.Any() && sourceData.Count<2)
                return;

            lock (_lock)
            {
                var overView = _adapter.OverviewOhlc;
                
                _chartZooms.Add(ReceiverZoom);
                _allZooms.AddRange(_chartZooms);

                var startpoint = Instant.FromDateTimeUtc(overView.Min(x => x.DateTimeUtc));
                var range = sourceData.GetTimeRange(ChartResolution);

                foreach (var z in _allZooms)
                {
                    z.StartPoint = startpoint;
                    z.ZoomToRange(range);
                }

                var resolver = _chartResolutionProvider = new ResolutionSourceProvider(() => ReceiverZoom.Resolution);

                // volume 

                var volchart = _volumeChart = new ChartViewModel(ChartGroupViewModel, ReceiverZoom, false);
                volchart.SeriesCollection.Add(sourceData.ToVolumeSeries(resolver, "Volume"));
                volchart.YAxesCollection.Add(GetYAxis("Volume"));

                // prices / scroller

                var priceChart = _priceChart = new ChartViewModel(ChartGroupViewModel, ReceiverZoom);
                priceChart.YAxesCollection.Add(GetYAxis("Price"));
                
                priceChart.SeriesCollection.Add(sourceData.ToGCandleSeries(resolver, "Prices"));

                //priceChart.SeriesCollection.Add(sourceData.ToSmaSeries(50, chartResolver2));

                ChartGroupViewModel.ScrollSeriesCollection.Add(overView.ToScrollSeries());
                ChartGroupViewModel.Charts.Add(volchart);
                ChartGroupViewModel.Charts.Add(priceChart);

                OverviewZoom.SetStartFrom(overView.MinOrDefault(x=>x.DateTimeUtc, DateTime.MinValue));

                OnDataUpdate?.Invoke(this, new OhlcDataUpdatedEvent(sourceData, _pair.Asset2, false));
            }
        }

        private void UpdateFromResolutionChange()
        {
            lock (_lock)
            {
                IsGraphReady = false;

                var newres = ChartGroupViewModel.ResolutionSelected;

                OverviewZoom.SetStartFrom(newres);
                
                TimeRange resetZoom = null;
                TimeRange newRange = null;

                var ts = newres.GetDefaultTimeSpan();
                var ep = OverviewZoom.EndPoint.ToDateTimeUtc();

                newRange = new TimeRange(ep, -ts, newres).RemoveLiveRange();
                resetZoom = new TimeRange(newRange.UtcFrom, newRange.UtcTo, OverviewZoom.Resolution);

                var priceData = RequestData(newRange);
                if (priceData.IsEmpty())
                    return;

                UiDispatcher.Invoke(() =>
                {
                    ClearData();

                    foreach (var cz in _chartZooms)
                        cz.Resolution = ChartGroupViewModel.ResolutionSelected;

                    MergeSeriesViews(priceData);

                    foreach (var cz in _chartZooms)
                    {
                        cz.SuspendRangeEventTill = DateTime.UtcNow.AddMilliseconds(200);
                        cz.Resolution = newRange.TimeResolution;
                        cz.ZoomToRange(newRange);
                    }

                    if (resetZoom!=null)
                    {
                        OverviewZoom.SuspendRangeEventTill = DateTime.UtcNow.AddMilliseconds(200);
                        OverviewZoom.ZoomToRange(resetZoom);
                    }

                    _lastLiveDataUpdate = DateTime.MinValue;

                    OnDataUpdate?.Invoke(this, new OhlcDataUpdatedEvent(priceData, _pair.Asset2, false));
                });
            }
        }

        private void InitLiveTimer()
        {
            _liveTimer = new Timer
            {
                Interval = 1000,
                AutoReset = false
            };

            _liveTimer.Elapsed += delegate (object o, ElapsedEventArgs args)
            {
                LiveUpdateElapsed(o, args);
                _liveTimer?.Start();
            };

            _liveTimer.Enabled = true;
        }

        private void UpdateData(bool isLive = false)
        {
            var r = isLive ? TimeRange.LiveRange(ReceiverZoom.Resolution) : ReceiverZoom.GetTimeRange();

            var nPriceData = RequestData(r);
            if (nPriceData.IsEmpty())
                return;

            UiDispatcher.Invoke(delegate
            {
                MergeSeriesViews(nPriceData);
                OnDataUpdate?.Invoke(this, new OhlcDataUpdatedEvent(nPriceData, _pair.Asset2, isLive));
                IsGraphReady = true;
            });

        }

        private OhlcData RequestData(TimeRange range)
        {
            if (_renderedCoverage.Covers(range))
                return null;

            SetDataStatus("Requesting Data");

            var priceData = _adapter.Request(range);
            if (priceData == null)
            {
                SetDataStatus("Data missing", false);
                return null;
            }

            _renderedCoverage.Include(range, priceData);

            SetDataStatus();
            return priceData;
        }

        private void ClearData()
        {
            _renderedCoverage = new CoverageMapMemory();

            // volume 
            _priceChart.SeriesCollection[0].Values.Clear();
            _volumeChart.SeriesCollection[0].Values.Clear();
        }

        private void MergeSeriesViews(OhlcData sourceData)
        {
            MergeSeriesViews<OhlcInstantChartPoint>(_priceChart.SeriesCollection[0], sourceData.ToGCandleSeries(_chartResolutionProvider, "Prices"));
            MergeSeriesViews<InstantChartPoint>(_volumeChart.SeriesCollection[0], sourceData.ToVolumeSeries(_chartResolutionProvider, "Volume"));
        }

        private void MergeSeriesViews<T>(ISeriesView oldData, ISeriesView newData) where T: IInstantChartPoint
        {
            var ov = oldData.Values.OfType<T>().ToList();
            foreach (var nd in newData.Values.OfType<T>())
            {
                var e = ov.FirstOrDefault(x => x.X == nd.X);
                if (e!=null)
                    oldData.Values.Remove(e);
                oldData.Values.Add(nd);
            }
        }

        private Axis GetYAxis(string title)
        {
            return new Axis
            {
                Title = title,
                LabelFormatter = LabelFormatter,
                Position = AxisPosition.RightTop,
                Sections = new SectionsCollection
                {
                    // Horizontal 0 value line
                    new AxisSection
                    {
                        Value = 0,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    }
                }
            };
        }

        public void QueueWork(Action action)
        {
            PrimeWpf.I.STAThreadPool.QueueWorkItem(a =>
            {
                try
                {
                    action.Invoke();
                }

#if DEBUG
                catch (Exception e)
                {
                    DataStatus = e.Message;
                }
#else
                catch {
                    DataStatus = "Fatal error";
                }
#endif
                return null;
            });
        }

        public TimeResolution ChartResolution => _chartZooms.FirstOrDefault().Resolution;

        private int _padLength = 10;
        private bool _isDataBusy;
        private bool _isGraphReady;

        private string LabelFormatter(double v)
        {
            var s = v.ToString();
            s = s.PadLeft(_padLength, ' ');
            return s;
        }

        public void SetDataStatus(string message = null, bool? busy = null)
        {
            UiDispatcher.Invoke(() =>
            {
                if (message == null)
                {
                    IsDataBusy = busy ?? false;
                    DataStatus = "Idle";
                }
                else
                {
                    IsDataBusy = busy ?? true;
                    DataStatus = message;
                }
            });
        }

        public override CommandContent GetPageCommand()
        {
            return new AssetGoCommand(_pair.Asset1);
        }

        public override void Dispose()
        {
            _liveTimer = null;

            ChartGroupViewModel.PropertyChanged -= OnChartGroupViewModelOnPropertyChanged;

            foreach (var zoom in _allZooms)
                zoom.OnRangePreviewChange -= OnZoomOnRangePreviewChange;

            base.Dispose();
        }
    }
}