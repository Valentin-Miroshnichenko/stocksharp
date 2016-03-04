namespace StockSharp.Designer
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Data;
	using System.Windows.Input;

	using Ecng.Common;
	using Ecng.Configuration;
	using Ecng.Serialization;
	using Ecng.Xaml;

	using MoreLinq;

	using StockSharp.Algo;
	using StockSharp.Algo.Candles;
	using StockSharp.Algo.Commissions;
	using StockSharp.Algo.Storages;
	using StockSharp.Algo.Strategies;
	using StockSharp.Algo.Testing;
	using StockSharp.BusinessEntities;
	using StockSharp.Configuration;
	using StockSharp.Designer.Commands;
	using StockSharp.Designer.Layout;
	using StockSharp.Localization;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.Studio.Core;
	using StockSharp.Studio.Core.Commands;
	using StockSharp.Xaml.Diagram;

	public partial class StrategyControl : IStudioCommandScope
	{
		#region DependencyProperty

		public static readonly DependencyProperty StrategyProperty = DependencyProperty.Register(nameof(Strategy), typeof(DiagramStrategy),
			typeof(StrategyControl), new PropertyMetadata(null, OnStrategyPropertyChanged));

		private static void OnStrategyPropertyChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			((StrategyControl)s).OnStrategyChanged((DiagramStrategy)e.OldValue, (DiagramStrategy)e.NewValue);
		}

		public DiagramStrategy Strategy
		{
			get { return (DiagramStrategy)GetValue(StrategyProperty); }
			set { SetValue(StrategyProperty, value); }
		}

		#endregion

		private readonly LayoutManager _layoutManager;

		private DiagramStrategy _strategy;

		public ICommand StartCommand { get; protected set; }

		public ICommand StopCommand { get; protected set; }

		public ICommand RefreshCompositionCommand { get; protected set; }

		public ICommand AddBreakpointCommand { get; private set; }

		public ICommand RemoveBreakpointCommand { get; private set; }

		public ICommand StepNextCommand { get; private set; }

		public ICommand StepIntoCommand { get; private set; }

		public ICommand StepOutCommand { get; private set; }

		public ICommand ContinueCommand { get; private set; }

		public StrategyControl()
		{
			InitializeComponent();
			InitializeCommands();

			_layoutManager = new LayoutManager(DockingManager);
			_layoutManager.Changed += RaiseChangedCommand;

			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();
			cmdSvc.Register<ControlChangedCommand>(this, false, cmd => RaiseChangedCommand());
			cmdSvc.Register<RequestBindSource>(this, true, cmd => RaiseBindStrategy(cmd.Control));
			cmdSvc.Register<OpenWindowCommand>(this, true, cmd =>
			{
				var ctrl = cmd.CtrlType.CreateInstance<IStudioControl>();

				if (cmd.IsToolWindow)
					_layoutManager.OpenToolWindow(ctrl);
				else
					_layoutManager.OpenDocumentWindow(ctrl);
			});
			cmdSvc.Register<LoadLayoutCommand>(this, true, cmd => _layoutManager.Load(cmd.Layout.LoadSettingsStorage()));
			cmdSvc.Register<SaveLayoutCommand>(this, true, cmd => cmd.Layout = _layoutManager.Save().SaveSettingsStorage());
		}

		private void InitializeCommands()
		{
			AddBreakpointCommand = new DelegateCommand(
				obj => new DebuggerAddBreakpointCommand().Process(_strategy),
				obj => new DebuggerAddBreakpointCommand().CanProcess(_strategy));

			RemoveBreakpointCommand = new DelegateCommand(
				obj => new DebuggerRemoveBreakpointCommand().Process(_strategy),
				obj => new DebuggerRemoveBreakpointCommand().CanProcess(_strategy));

			StepNextCommand = new DelegateCommand(
				obj => new DebuggerStepNextCommand().Process(_strategy),
				obj => new DebuggerStepNextCommand().CanProcess(_strategy));

			StepIntoCommand = new DelegateCommand(
				obj => new DebuggerStepIntoCommand().Process(_strategy),
				obj => new DebuggerStepIntoCommand().CanProcess(_strategy));

			StepOutCommand = new DelegateCommand(
				obj => new DebuggerStepOutCommand().Process(_strategy),
				obj => new DebuggerStepOutCommand().CanProcess(_strategy));

			ContinueCommand = new DelegateCommand(
				obj => new DebuggerContinueCommand().Process(_strategy),
				obj => new DebuggerContinueCommand().CanProcess(_strategy));
		}

		protected void Reset()
		{
			new ResetedCommand().Process(Strategy);
		}

		private void OnStrategyChanged(DiagramStrategy oldStrategy, DiagramStrategy newStrategy)
		{
			if (oldStrategy != null)
			{
				ConfigManager
					.GetService<LogManager>()
					.Sources
					.Remove(oldStrategy);

				ConfigManager
					.GetService<IStudioCommandService>()
					.UnBind(newStrategy);

				oldStrategy.Composition = null;

				oldStrategy.ParametersChanged -= RaiseChangedCommand;

				oldStrategy.OrderRegistering += OnStrategyOrderRegistering;
				oldStrategy.OrderReRegistering += OnStrategyOrderReRegistering;
				oldStrategy.OrderRegisterFailed += OnStrategyOrderRegisterFailed;

				oldStrategy.StopOrderRegistering += OnStrategyOrderRegistering;
				oldStrategy.StopOrderReRegistering += OnStrategyOrderReRegistering;
				oldStrategy.StopOrderRegisterFailed += OnStrategyOrderRegisterFailed;

				oldStrategy.NewMyTrades += OnStrategyNewMyTrade;
			}

			_strategy = newStrategy;

			if (_strategy == null)
				return;

			Key = $"_{_strategy.Id.ToString("N")}";

			ConfigManager
				.GetService<LogManager>()
				.Sources
				.Add(newStrategy);

			_strategy.ParametersChanged += RaiseChangedCommand;

			_strategy.OrderRegistering += OnStrategyOrderRegistering;
			_strategy.OrderReRegistering += OnStrategyOrderReRegistering;
			_strategy.OrderRegisterFailed += OnStrategyOrderRegisterFailed;

			_strategy.StopOrderRegistering += OnStrategyOrderRegistering;
			_strategy.StopOrderReRegistering += OnStrategyOrderReRegistering;
			_strategy.StopOrderRegisterFailed += OnStrategyOrderRegisterFailed;

			_strategy.NewMyTrades += OnStrategyNewMyTrade;

			_strategy.PnLChanged += () =>
			{
				new PnLChangedCommand(_strategy.CurrentTime, _strategy.PnL - (_strategy.Commission ?? 0), _strategy.PnLManager.UnrealizedPnL, _strategy.Commission).Process(_strategy);
			};

			//_strategy.PositionChanged += () => new PositionCommand(_strategy.CurrentTime, _strategy.Position, false).Process(_strategy);

			ConfigManager
				.GetService<IStudioCommandService>()
				.Bind(_strategy, this);

			RaiseBindStrategy();
		}

		private void OnStrategyOrderRegisterFailed(OrderFail fail)
		{
			new OrderFailCommand(fail, OrderActions.Registering).Process(_strategy);
		}

		private void OnStrategyOrderReRegistering(Order oldOrder, Order newOrder)
		{
			new ReRegisterOrderCommand(oldOrder, newOrder).Process(_strategy);
		}

		private void OnStrategyOrderRegistering(Order order)
		{
			new OrderCommand(order, OrderActions.Registering).Process(_strategy);
		}

		private void OnStrategyNewMyTrade(IEnumerable<MyTrade> trades)
		{
			new NewMyTradesCommand(trades).Process(_strategy);
		}

		private void RaiseBindStrategy(IStudioControl control = null)
		{
			new BindStrategyCommand(_strategy, control).SyncProcess(_strategy);
		}

		#region IPersistable

		public override void Load(SettingsStorage storage)
		{
			storage.TryLoadSettings<SettingsStorage>("LayoutManager", s => _layoutManager.Load(s));
		}

		public override void Save(SettingsStorage storage)
		{
			storage.SetValue("LayoutManager", _layoutManager.Save());
		}

		#endregion

		public override void Dispose()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();
			cmdSvc.UnRegister<ControlChangedCommand>(this);
			cmdSvc.UnRegister<RequestBindSource>(this);
			cmdSvc.UnRegister<OpenWindowCommand>(this);
			cmdSvc.UnRegister<LoadLayoutCommand>(this);
			cmdSvc.UnRegister<SaveLayoutCommand>(this);
		}
	}

	public class LiveStrategyControl : StrategyControl
	{
		public LiveStrategyControl()
		{
			InitializeCommands();

			this.SetBindings(TitleProperty, this, "Strategy.Composition.Name", BindingMode.OneWay, new TitleConverter(LocalizedStrings.Str3176));
		}

		private void InitializeCommands()
		{
			RefreshCompositionCommand = new DelegateCommand(
				obj => Load(this.Save()),
				obj => Strategy != null && Strategy.ProcessState == ProcessStates.Stopped);

			StartCommand = new DelegateCommand(
				obj =>
				{
					var connector = ConfigManager.GetService<IConnector>();

					Strategy.Connector = connector;

					Strategy.SetCandleManager(new CandleManager(connector));
					Strategy.Start();
				},
				obj => Strategy != null && Strategy.ProcessState == ProcessStates.Stopped);

			StopCommand = new DelegateCommand(
				obj => Strategy.Start(),
				obj => Strategy != null && Strategy.ProcessState == ProcessStates.Started);
		}

		public override bool CanClose()
		{
			if (Strategy == null || Strategy.ProcessState == ProcessStates.Stopped)
				return true;

			new MessageBoxBuilder()
				.Owner(this)
				.Caption(Title)
				.Text(LocalizedStrings.Str3617Params.Put(Title))
				.Icon(MessageBoxImage.Warning)
				.Button(MessageBoxButton.OK)
				.Show();

			return false;
		}

		#region IPersistable

		public override void Load(SettingsStorage storage)
		{
			var compositionId = storage.GetValue<Guid>("CompositionId");
			var registry = ConfigManager.GetService<StrategiesRegistry>();
			var composition = (CompositionDiagramElement)registry.Strategies.FirstOrDefault(c => c.TypeId == compositionId);

			var strategy = new DiagramStrategy
			{
				Id = storage.GetValue<Guid>("StrategyId"),
				Composition = registry.Clone(composition)
			};
			strategy.Load(storage);

			Strategy = strategy;

			base.Load(storage);
		}

		public override void Save(SettingsStorage storage)
		{
			if (Strategy != null)
			{
				storage.SetValue("CompositionId", Strategy.Composition.TypeId);
				storage.SetValue("StrategyId", Strategy.Id);

				Strategy.Save(storage);
			}

			base.Save(storage);
		}

		#endregion
	}

	public class EmulationStrategyControl : StrategyControl
	{
		private HistoryEmulationConnector _connector;

		public EmulationStrategyControl()
		{
			InitializeCommands();

			this.SetBindings(TitleProperty, this, "Strategy.Composition.Name", BindingMode.OneWay, new TitleConverter(LocalizedStrings.Str1174));
		}

		private void InitializeCommands()
		{
			RefreshCompositionCommand = new DelegateCommand(
				obj => Load(this.Save()), 
				obj => _connector == null || _connector.State == EmulationStates.Stopped);

			StartCommand = new DelegateCommand(
				obj =>
				{
					try
					{
						StartEmulation();
					}
					catch (Exception excp)
					{
						StopEmulation();

						new MessageBoxBuilder()
							.Owner(this)
							.Caption(Title)
							.Text(excp.Message)
							.Icon(MessageBoxImage.Warning)
							.Button(MessageBoxButton.OK)
							.Show();
					}
				},
				obj => _connector == null || _connector.State == EmulationStates.Stopped);

			StopCommand = new DelegateCommand(
				obj => StopEmulation(),
				obj => _connector != null && _connector.State == EmulationStates.Started);
		}

		public override bool CanClose()
		{
			if (_connector == null || _connector.State == EmulationStates.Stopped)
				return true;

			new MessageBoxBuilder()
				.Owner(this)
				.Caption(Title)
				.Text(LocalizedStrings.Str3617Params.Put(Title))
				.Icon(MessageBoxImage.Warning)
				.Button(MessageBoxButton.OK)
				.Show();

			return false;
		}

		private void StartEmulation()
		{
			if (_connector != null && _connector.State != EmulationStates.Stopped)
				throw new InvalidOperationException(LocalizedStrings.Str3015);

			if (Strategy == null)
				throw new InvalidOperationException("Strategy not selected.");

			var strategy = (EmulationDiagramStrategy)Strategy;
			var settings = strategy.EmulationSettings;

			if (settings.MarketDataSettings == null)
				throw new InvalidOperationException(LocalizedStrings.Str3014);

			new SetDefaultEmulationSettingsCommand(settings).Process(this);

			strategy
				.Composition
				.Parameters
				.ForEach(p =>
				{
					if (p.Type == typeof(Security) && p.Value == null)
						throw new InvalidOperationException(LocalizedStrings.Str1380);
				});

			strategy.Reset();
			Reset();

			var securityId = "empty@empty";
			var secGen = new SecurityIdGenerator();
			var secIdParts = secGen.Split(securityId);
			var secCode = secIdParts.SecurityCode;
			var board = ExchangeBoard.GetOrCreateBoard(secIdParts.BoardCode);
			var timeFrame = settings.CandlesTimeFrame;
			var useCandles = settings.MarketDataSource == MarketDataSource.Candles;

			// create test security
			var security = new Security
			{
				Id = securityId, // sec id has the same name as folder with historical data
				Code = secCode,
				Board = board,
			};

			// storage to historical data
			var storageRegistry = new StudioStorageRegistry
			{
				MarketDataSettings = settings.MarketDataSettings
			};

			var startTime = settings.StartDate.ChangeKind(DateTimeKind.Utc);
			var stopTime = settings.StopDate.ChangeKind(DateTimeKind.Utc);

			// ProgressBar refresh step
			var progressStep = ((stopTime - startTime).Ticks / 100).To<TimeSpan>();

			// set ProgressBar bounds
			TicksAndDepthsProgress.Value = 0;
			TicksAndDepthsProgress.Maximum = 100;

			// test portfolio
			var portfolio = new Portfolio
			{
				Name = "test account",
				BeginValue = 1000000,
			};

			var securityProvider = ConfigManager.GetService<ISecurityProvider>();

			// create backtesting connector
			_connector = new HistoryEmulationConnector(securityProvider, new[] { portfolio }, new StorageRegistry())
			{
				EmulationAdapter =
				{
					Emulator =
					{
						Settings =
						{
							// match order if historical price touched our limit order price. 
							// It is terned off, and price should go through limit order price level
							// (more "severe" test mode)
							MatchOnTouch = settings.MatchOnTouch, 
							IsSupportAtomicReRegister = settings.IsSupportAtomicReRegister,
							Latency = settings.EmulatoinLatency,
						}
					}
				},

				UseExternalCandleSource = useCandles,

				HistoryMessageAdapter =
				{
					StorageRegistry = storageRegistry,
					StorageFormat = settings.StorageFormat,

					// set history range
					StartDate = startTime,
					StopDate = stopTime,
				},

				// set market time freq as time frame
				MarketTimeChangedInterval = timeFrame,
			};

			((ILogSource)_connector).LogLevel = settings.DebugLog ? LogLevels.Debug : LogLevels.Info;

			ConfigManager.GetService<LogManager>().Sources.Add(_connector);

			strategy.Volume = 1;
			strategy.Portfolio = portfolio;
			strategy.Security = security;
			strategy.Connector = _connector;
			strategy.LogLevel = settings.DebugLog ? LogLevels.Debug : LogLevels.Info;

			// by default interval is 1 min,
			// it is excessively for time range with several months
			strategy.UnrealizedPnLInterval = ((stopTime - startTime).Ticks / 1000).To<TimeSpan>();

			strategy.SetCandleManager(new CandleManager(_connector));

			_connector.NewSecurity += s =>
			{
				//TODO send real level1 message
				var level1Info = new Level1ChangeMessage
				{
					SecurityId = s.ToSecurityId(),
					ServerTime = startTime,
				}
					.TryAdd(Level1Fields.PriceStep, secIdParts.SecurityCode == "RIZ2" ? 10m : 1)
					.TryAdd(Level1Fields.StepPrice, 6m)
					.TryAdd(Level1Fields.MinPrice, 10m)
					.TryAdd(Level1Fields.MaxPrice, 1000000m)
					.TryAdd(Level1Fields.MarginBuy, 10000m)
					.TryAdd(Level1Fields.MarginSell, 10000m);

				// fill level1 values
				_connector.SendInMessage(level1Info);

				if (settings.UseMarketDepths)
				{
					_connector.RegisterMarketDepth(security);

					if (
							// if order book will be generated
							settings.GenerateDepths ||
							// of backtesting will be on candles
							useCandles
						)
					{
						// if no have order book historical data, but strategy is required,
						// use generator based on last prices
						_connector.RegisterMarketDepth(new TrendMarketDepthGenerator(_connector.GetSecurityId(s))
						{
							Interval = TimeSpan.FromSeconds(1), // order book freq refresh is 1 sec
							MaxAsksDepth = settings.MaxDepths,
							MaxBidsDepth = settings.MaxDepths,
							UseTradeVolume = true,
							MaxVolume = settings.MaxVolume,
							MinSpreadStepCount = 2, // min spread generation is 2 pips
							MaxSpreadStepCount = 5, // max spread generation size (prevent extremely size)
							MaxPriceStepCount = 3   // pips size,
						});
					}
				}
			};

			var nextTime = startTime + progressStep;

			// handle historical time for update ProgressBar
			_connector.MarketTimeChanged += d =>
			{
				if (_connector.CurrentTime < nextTime && _connector.CurrentTime < stopTime)
					return;

				var steps = (_connector.CurrentTime - startTime).Ticks / progressStep.Ticks + 1;
				nextTime = startTime + (steps * progressStep.Ticks).To<TimeSpan>();
				this.GuiAsync(() => TicksAndDepthsProgress.Value = steps);
			};

			_connector.LookupSecuritiesResult += (ss) =>
			{
				if (strategy.ProcessState != ProcessStates.Stopped)
					return;

				// start strategy before emulation started
				strategy.Start();

				// start historical data loading when connection established successfully and all data subscribed
				_connector.Start();
			};

			_connector.StateChanged += () =>
			{
				switch (_connector.State)
				{
					case EmulationStates.Stopped:
						strategy.Stop();

						this.GuiAsync(() =>
						{
							if (_connector.IsFinished)
								TicksAndDepthsProgress.Value = TicksAndDepthsProgress.Maximum;
						});
						break;
					case EmulationStates.Started:
						break;
				}
			};

			_connector.Disconnected += () =>
			{
				this.GuiAsync(() => _connector.Dispose());
			};

			TicksAndDepthsProgress.Value = 0;

			new DebuggerStateCommand(true).Process(strategy);

			// raise NewSecurities and NewPortfolio for full fill strategy properties
			_connector.Connect();

			// 1 cent commission for trade
			_connector.SendInMessage(new CommissionRuleMessage
			{
				Rule = new CommissionPerTradeRule
				{
					Value = 0.01m
				}
			});
		}

		private void StopEmulation()
		{
			_connector?.Disconnect();

			new DebuggerStateCommand(false).Process(Strategy);
			new DebuggerContinueCommand().Process(Strategy);
		}

		#region IPersistable

		public override void Load(SettingsStorage storage)
		{
			var strategy = new EmulationDiagramStrategy();
			strategy.Load(storage);

			Strategy = strategy;

			base.Load(storage);
		}

		public override void Save(SettingsStorage storage)
		{
			Strategy?.Save(storage);

			base.Save(storage);
		}

		#endregion
	}
}
