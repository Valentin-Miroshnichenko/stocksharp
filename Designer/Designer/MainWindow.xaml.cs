#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleDiagram.SampleDiagramPublic
File: MainWindow.xaml.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Designer
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Media.Imaging;

	using DevExpress.Xpf.Core;
	using DevExpress.Xpf.Docking;
	using DevExpress.Xpf.Docking.Base;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.Configuration;
	using Ecng.Interop;
	using Ecng.Serialization;
	using Ecng.Xaml;

	using StockSharp.Algo;
	using StockSharp.Algo.History.Hydra;
	using StockSharp.Algo.Storages;
	using StockSharp.BusinessEntities;
	using StockSharp.Community;
	using StockSharp.Configuration;
	using StockSharp.Designer.Commands;
	using StockSharp.Designer.Layout;
	using StockSharp.Localization;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.Studio.Controls;
	using StockSharp.Studio.Core;
	using StockSharp.Studio.Core.Commands;
	using StockSharp.Studio.Core.Services;
	using StockSharp.Xaml;
	using StockSharp.Xaml.Diagram;

	public partial class MainWindow
	{
		public static RoutedCommand ConnectorSettingsCommand = new RoutedCommand();
		public static RoutedCommand ConnectDisconnectCommand = new RoutedCommand();
		public static RoutedCommand RefreshCompositionCommand = new RoutedCommand();
		public static RoutedCommand HelpCommand = new RoutedCommand();
		public static RoutedCommand AboutCommand = new RoutedCommand();
		public static RoutedCommand TargetPlatformCommand = new RoutedCommand();
		public static RoutedCommand ResetSettingsCommand = new RoutedCommand();
		public static RoutedCommand AddNewSecurityCommand = new RoutedCommand();

		private StrategiesRegistry _strategiesRegistry;
		private Connector _connector;
		private LayoutManager _layoutManager;
		private MarketDataSettingsCache _marketDataSettingsCache;
		private EmulationSettings _emulationSettings;

		private object ActiveLayoutContent => (DockingManager.ActiveLayoutItem as LayoutPanel)?.Content;

		private readonly SessionClient _sessionClient = new SessionClient();

		public MainWindow()
		{
			UserConfig.Instance.SuspendChangesMonitor();
			UserConfig.Instance.SaveLayout = () => GuiDispatcher.GlobalDispatcher.AddSyncAction(() => _layoutManager.Save());

			ConfigManager.RegisterService(UserConfig.Instance.CreateLogger());
			ConfigManager.RegisterService<IStudioCommandService>(new StudioCommandService());
			ConfigManager.RegisterService<IPersistableService>(UserConfig.Instance);

			InitializeComponent();
			Title = TypeHelper.ApplicationNameWithVersion;

			InitializeLayoutManager();
			InitializeDataSource();
			InitializeMarketDataSettingsCache();
			InitializeEmulationSettings();
			InitializeCommands();
			InitializeConnector();
			InitializeStrategiesRegistry();

			ThemeManager.ApplicationThemeChanged += (s, e) => UserConfig.Instance.SetValue("ThemeName", ThemeManager.ApplicationThemeName);
		}

		private static void InitializeDataSource()
		{
			((EntityRegistry)ConfigManager.GetService<IEntityRegistry>()).FirstTimeInit(Properties.Resources.StockSharp);
		}

		private void InitializeCommands()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();

			cmdSvc.Register<OpenMarketDataSettingsCommand>(this, true, cmd => OpenMarketDataPanel(cmd.Settings));
			cmdSvc.Register<ControlChangedCommand>(this, true, cmd => _layoutManager.MarkControlChanged(cmd.Control));

			#region RefreshSecurities

			cmdSvc.Register<RefreshSecurities>(this, false, cmd => ThreadingHelper
				.Thread(() =>
				{
					var entityRegistry = ConfigManager.GetService<IEntityRegistry>();
					var count = 0;
					var progress = 0;

					try
					{
						using (var client = new RemoteStorageClient(new Uri(cmd.Settings.Path)))
						{
							var credentials = cmd.Settings.Credentials;

							client.Credentials.Login = credentials.Login;
							client.Credentials.Password = credentials.Password;

							foreach (var secType in cmd.Types.TakeWhile(secType => !cmd.IsCancelled()))
							{
								if (secType == SecurityTypes.Future)
								{
									var from = DateTime.Today.AddMonths(-4);
									var to = DateTime.Today.AddMonths(4);
									var expiryDates = from.GetExpiryDates(to);

									foreach (var expiryDate in expiryDates.TakeWhile(d => !cmd.IsCancelled()))
									{
										client.Refresh(entityRegistry.Securities, new Security { Type = secType, ExpiryDate = expiryDate }, s =>
										{
											entityRegistry.Securities.Save(s);
											_connector.SendOutMessage(s.ToMessage());
											count++;
										}, cmd.IsCancelled);
									}
								}
								else
								{
									// для акций передаем фиктивное значение ExpiryDate, чтобы получить инструменты без даты экспирации
									var expiryDate = secType == SecurityTypes.Stock ? DateTime.Today : (DateTime?)null;

									client.Refresh(entityRegistry.Securities, new Security { Type = secType, ExpiryDate = expiryDate }, s =>
									{
										entityRegistry.Securities.Save(s);
										_connector.SendOutMessage(s.ToMessage());
										count++;
									}, cmd.IsCancelled);
								}

								cmd.ProgressChanged(++progress);
							}
						}
					}
					catch (Exception ex)
					{
						ex.LogError();
					}

					if (cmd.IsCancelled())
						return;

					try
					{
						cmd.WhenFinished(count);
					}
					catch (Exception ex)
					{
						ex.LogError();
					}
				})
				.Launch());

			#endregion

			#region CreateSecurityCommand

			cmdSvc.Register<CreateSecurityCommand>(this, true, cmd =>
			{
				var entityRegistry = ConfigManager.GetService<IEntityRegistry>();

				ISecurityWindow wnd;

				if (cmd.SecurityType == typeof(Security))
					wnd = new SecurityCreateWindow();
				else
					throw new InvalidOperationException(LocalizedStrings.Str2140Params.Put(cmd.SecurityType));

				wnd.ValidateId = id =>
				{
					if (entityRegistry.Securities.ReadById(id) != null)
						return LocalizedStrings.Str2927Params.Put(id);

					return null;
				};

				if (!((Window)wnd).ShowModal(Application.Current.GetActiveOrMainWindow()))
					return;

				entityRegistry.Securities.Save(wnd.Security);
				_connector.SendOutMessage(wnd.Security.ToMessage());
				cmd.Security = wnd.Security;
			});

			#endregion

			cmdSvc.Register<SetDefaultEmulationSettingsCommand>(this, false, cmd =>
			{
				var storage = cmd.Settings.Save();

                _emulationSettings.Load(storage);
				UserConfig.Instance.SetValue("EmulationSettings", storage);
			});
			cmdSvc.Register<OpenWindowCommand>(this, true, cmd =>
			{
				var ctrl = cmd.CtrlType.CreateInstance<IStudioControl>();

				if (cmd.IsToolWindow)
					_layoutManager.OpenToolWindow(ctrl);
				else
					_layoutManager.OpenDocumentWindow(ctrl);
			});
			cmdSvc.Register<OpenBacktestingCommand>(this, true, cmd => OpenEmulation(cmd.Element));
			cmdSvc.Register<OpenLiveCommand>(this, true, cmd => OpenLive(cmd.Element));

			cmdSvc.Register<AddCompositionCommand>(this, true, cmd =>
			{
				var element = new CompositionDiagramElement
				{
					Name = "New " + cmd.Type.ToString().ToLower()
				};
				var item = new CompositionItem(cmd.Type, element);

				_strategiesRegistry.Save(item);

				OpenComposition(item);
			});
			cmdSvc.Register<OpenCompositionCommand>(this, true, cmd => OpenComposition(cmd.Element));
			cmdSvc.Register<RemoveCompositionCommand>(this, true, cmd =>
			{
				var item = cmd.Element;

				var control = _layoutManager
					.DockingControls
					.OfType<DiagramEditorPanel>()
					.FirstOrDefault(c => c.Key.CompareIgnoreCase(item.Key));

				if (control != null)
				{
					control.ResetIsChanged();
					_layoutManager.CloseWindow(control);
				}

				_strategiesRegistry.Remove(item);
			});

			cmdSvc.Register<SaveCompositionCommand>(this, true, cmd => _strategiesRegistry.Save(cmd.Element));
			cmdSvc.Register<DiscardCompositionCommand>(this, true, cmd => _strategiesRegistry.Discard(cmd.Element));
			cmdSvc.Register<RefreshCompositionCommand>(this, true, cmd => _strategiesRegistry.Reload(cmd.Element));

			cmdSvc.Register<RequestBindSource>(this, true, cmd => new BindConnectorCommand(ConfigManager.GetService<IConnector>(), cmd.Control).SyncProcess(this));
		}

		private void InitializeMarketDataSettingsCache()
		{
			_marketDataSettingsCache = new MarketDataSettingsCache();

			_marketDataSettingsCache.Settings.Add(new MarketDataSettings
			{
				Id = Guid.Parse("93B222AB-9196-410F-8998-D44610ECC65B"),
				Path = @"..\..\..\..\Samples\Testing\HistoryData\".ToFullPath(),
                UseLocal = true,
			});
			_marketDataSettingsCache.Settings.Add(MarketDataSettings.StockSharpSettings);

			_marketDataSettingsCache.Changed += () =>
			{
				UserConfig.Instance.SetValue("MarketDataSettingsCache", ((IPersistable)_marketDataSettingsCache).Save());
			};

			ConfigManager.RegisterService(_marketDataSettingsCache);
		}

		private void InitializeEmulationSettings()
		{
			_emulationSettings = new EmulationSettings
			{
				MarketDataSettings = _marketDataSettingsCache.Settings.FirstOrDefault()
			};
		}

		private void InitializeConnector()
		{
			var entityRegistry = ConfigManager.GetService<IEntityRegistry>();
			var storageRegistry = ConfigManager.GetService<IStorageRegistry>();

			_connector = new Connector(entityRegistry, storageRegistry)
			{
				StorageAdapter =
				{
					DaysLoad = TimeSpan.Zero
				}
			};
			_connector.Connected += ConnectorOnConnectionStateChanged;
			_connector.Disconnected += ConnectorOnConnectionStateChanged;
			_connector.ConnectionError += ConnectorOnConnectionError;
			ConfigManager.GetService<LogManager>().Sources.Add(_connector);
			ConfigManager.RegisterService<IConnector>(_connector);
			ConfigManager.RegisterService<ISecurityProvider>(_connector);
			ConfigManager.RegisterService<IPortfolioProvider>(_connector);
		}

		private void InitializeLayoutManager()
		{
			_layoutManager = new LayoutManager(DockingManager, DocumentHost);
			_layoutManager.Changed += UserConfig.Instance.MarkLayoutChanged;
			ConfigManager.GetService<LogManager>().Sources.Add(_layoutManager);
			ConfigManager.RegisterService(_layoutManager);
		}

		private void InitializeStrategiesRegistry()
		{
			var compositionsPath = Path.Combine(BaseApplication.AppDataPath, "Compositions");
			var strategiesPath = Path.Combine(BaseApplication.AppDataPath, "Strategies");

			_strategiesRegistry = new StrategiesRegistry(compositionsPath, strategiesPath);
			ConfigManager.GetService<LogManager>().Sources.Add(_strategiesRegistry);
			_strategiesRegistry.Init();
			ConfigManager.RegisterService(_strategiesRegistry);
		}

		#region Event handlers

		private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
		{
			_sessionClient.CreateSession(Products.Designer);

			foreach (var type in AppConfig.Instance.ToolControls.GetControlTypes())
				RibbonToolControlsGroup.AddToolControl(type, this);

			LoadSettings();
			
			_connector.StorageAdapter.Load();
		}

		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			foreach (var control in _layoutManager.DockingControls.OfType<BaseStudioControl>())
				control.CanClose();

			_layoutManager.Dispose();
			UserConfig.Instance.Dispose();

			_sessionClient.CloseSession();
		}

		private void DockingManager_OnLayoutItemActivated(object sender, LayoutItemActivatedEventArgs ea)
		{
			DockItemActivated((ea.Item as LayoutPanel)?.Content);
		}

		private void DockingManager_OnDockItemActivated(object sender, DockItemActivatedEventArgs ea)
		{
			DockItemActivated((ea.Item as LayoutPanel)?.Content);
		}

		private void DockingManager_OnDockItemClosed(object sender, DockItemClosedEventArgs e)
		{
			DockItemActivated(ActiveLayoutContent);
		}

		private void ConnectorOnConnectionStateChanged()
		{
			this.GuiAsync(() =>
			{
				var uri = _connector.ConnectionState == ConnectionStates.Disconnected
							  ? "pack://application:,,,/Designer;component/Images/Connect_24x24.png"
							  : "pack://application:,,,/Designer;component/Images/Disconnect_24x24.png";

				ConnectButton.Glyph = new BitmapImage(new Uri(uri));
			});
		}

		private void ConnectorOnConnectionError(Exception obj)
		{
			this.GuiAsync(() =>
			{
				new MessageBoxBuilder()
					.Owner(this)
					.Caption(Title)
					.Text(LocalizedStrings.Str626)
					.Button(MessageBoxButton.OK)
					.Icon(MessageBoxImage.Warning)
					.Show();

				_connector.Disconnect();
			});
		}

		#endregion

		#region Commands

		private void ConnectorSettingsCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = _connector.ConnectionState == ConnectionStates.Disconnected ||
			               _connector.ConnectionState == ConnectionStates.Failed;
		}

		private void ConnectorSettingsCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			ConfigureConnector();
		}

		private void ConnectDisconnectCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = _connector.ConnectionState == ConnectionStates.Connected ||
			               _connector.ConnectionState == ConnectionStates.Disconnected ||
			               _connector.ConnectionState == ConnectionStates.Failed;
		}

		private void ConnectDisconnectCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (_connector.ConnectionState != ConnectionStates.Connected)
			{
				var innerAdapters = _connector.Adapter.InnerAdapters;

				if (innerAdapters.IsEmpty())
				{
					new MessageBoxBuilder()
						.Owner(this)
						.Text(LocalizedStrings.Str3650)
						.Warning()
						.Show();

					if (!ConfigureConnector())
						return;
				}

				if (innerAdapters.SortedAdapters.IsEmpty())
				{
					new MessageBoxBuilder()
						.Owner(this)
						.Text(LocalizedStrings.Str3651)
						.Warning()
						.Show();

					if (!ConfigureConnector())
						return;
				}

				_connector.Connect();
			}
			else
				_connector.Disconnect();
		}

		private void HelpCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			"http://stocksharp.com/forum/yaf_postst5874_S--Designer---coming-soon.aspx".To<Uri>().OpenLinkInBrowser();
		}

		private void AboutCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new AboutWindow(this);
			wnd.ShowModal(this);
		}

		private void TargetPlatformCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var language = LocalizedStrings.ActiveLanguage;
			var platform = Environment.Is64BitProcess ? Platforms.x64 : Platforms.x86;

			var window = new TargetPlatformWindow();

			if (!window.ShowModal(this))
				return;

			if (window.SelectedLanguage == language && window.SelectedPlatform == platform)
				return;

			// temporarily set prev lang for display the followed message
			// and leave all text as is if user will not restart the app
			LocalizedStrings.ActiveLanguage = language;

			var result = new MessageBoxBuilder()
				.Text(LocalizedStrings.Str2952Params.Put(TypeHelper.ApplicationName))
				.Owner(this)
				.Info()
				.YesNo()
				.Show();

			if (result == MessageBoxResult.Yes)
				Application.Current.Restart();
		}

		private void ResetSettingsCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var res = new MessageBoxBuilder()
						.Text(LocalizedStrings.Str2954Params.Put(TypeHelper.ApplicationName))
						.Warning()
						.Owner(this)
						.YesNo()
						.Show();

			if (res != MessageBoxResult.Yes)
				return;

			UserConfig.Instance.ResetSettings();
			Application.Current.Restart();
		}

		private void AddNewSecurityCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			new CreateSecurityCommand((Type)e.Parameter).Process(this, true);
		}

		#endregion

		private void OpenComposition(CompositionItem item)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			var content = new DiagramEditorPanel
			{
				Composition = item
			};

            _layoutManager.OpenDocumentWindow(content);
		}

		private void OpenEmulation(CompositionItem item)
		{
			var strategy = new EmulationDiagramStrategy
			{
				Composition = _strategiesRegistry.Clone(item.Element),
				EmulationSettings = _emulationSettings.Clone()
			};

			var content = new EmulationStrategyControl
			{
				Strategy = strategy
			};

			_layoutManager.OpenDocumentWindow(content);

			new LoadLayoutCommand(Properties.Resources.DefaultStrategyLayout).Process(strategy);
		}

		private void OpenLive(CompositionItem item)
		{
			var strategy = new DiagramStrategy
			{
				Composition = _strategiesRegistry.Clone(item.Element)
			};

			var content = new LiveStrategyControl
			{
				Strategy = strategy
			};

			_layoutManager.OpenDocumentWindow(content);

			new LoadLayoutCommand(Properties.Resources.DefaultStrategyLayout).Process(strategy);
		}

		private void OpenMarketDataPanel(MarketDataSettings settings)
		{
			var content = new MarketDataPanel
			{
				SelectedSettings = settings
			};

			_layoutManager.OpenDocumentWindow(content);
		}

		private void LoadSettings()
		{
			var settings = UserConfig.Instance;

			settings.TryLoadSettings<SettingsStorage>("MarketDataSettingsCache", s => _marketDataSettingsCache.Load(s));
			settings.TryLoadSettings<SettingsStorage>("EmulationSettings", s => _emulationSettings.Load(s));
			settings.TryLoadSettings<SettingsStorage>("Connector", s => _connector.Load(s));

			ThemeManager.ApplicationThemeName = settings.GetValue("ThemeName", "Office2016Black");

			var layout = settings.GetValue("Layout", Properties.Resources.DefaultAppLayout.LoadSettingsStorage());
			_layoutManager.Load(layout);
			
			settings.TryLoadSettings<string>("Ribbon", s => Ribbon.LoadDevExpressControl(s));

			settings.ResumeChangesMonitor();
		}

		private bool ConfigureConnector()
		{
			var result = _connector.Configure(this);

			if (!result)
				return false;

			UserConfig.Instance.SetValue("Connector", _connector.Save());

			return true;
		}

		private void DockItemActivated(object control)
		{
			if (control == null)
			{
				RibbonSchemasTab.DataContext = null;
				RibbonEmulationTab.DataContext = null;
				RibbonLiveTab.DataContext = null;
				RibbonDesignerTab.DataContext = null;
				Ribbon.SelectedPage = RibbonCommonTab;

				return;
			}

			control
				.DoIf<object, DiagramEditorPanel>(editor =>
				{
					RibbonSchemasTab.DataContext = null;
					RibbonEmulationTab.DataContext = null;
					RibbonLiveTab.DataContext = null;
					RibbonDesignerTab.DataContext = editor;
					Ribbon.SelectedPage = RibbonDesignerTab;
				});

			control
				.DoIf<object, EmulationStrategyControl>(editor =>
				{
					RibbonSchemasTab.DataContext = null;
					RibbonDesignerTab.DataContext = null;
					RibbonLiveTab.DataContext = null;
					RibbonEmulationTab.DataContext = editor;
					Ribbon.SelectedPage = RibbonEmulationTab;
				});

			control
				.DoIf<object, LiveStrategyControl>(editor =>
				{
					RibbonSchemasTab.DataContext = null;
					RibbonEmulationTab.DataContext = null;
					RibbonDesignerTab.DataContext = null;
					RibbonLiveTab.DataContext = editor;
					Ribbon.SelectedPage = RibbonLiveTab;
				});

			control
				.DoIf<object, SolutionExplorerPanel>(editor =>
				{
					RibbonSchemasTab.DataContext = editor;
					RibbonEmulationTab.DataContext = null;
					RibbonLiveTab.DataContext = null;
					RibbonDesignerTab.DataContext = null;
					Ribbon.SelectedPage = RibbonSchemasTab;
				});
		}
	}
}