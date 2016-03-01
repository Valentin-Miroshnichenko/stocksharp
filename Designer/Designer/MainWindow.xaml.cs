﻿#region S# License
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
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media.Imaging;

	using DevExpress.Xpf.Core;
	using DevExpress.Xpf.Docking;
	using DevExpress.Xpf.Docking.Base;
	using DevExpress.Xpf.Editors.Helpers;
	using DevExpress.Xpf.Editors.Internal;
	using DevExpress.Xpf.Editors.Popups;
	using DevExpress.Xpf.Editors.Settings;

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
		sealed class PersistableService : IPersistableService
		{
			public bool ContainsKey(string key)
			{
				return false;
			}

			public TValue GetValue<TValue>(string key, TValue defaultValue = default(TValue))
			{
				return defaultValue;
			}

			public void SetValue(string key, object value)
			{
			}
		}

		public static RoutedCommand AddCommand = new RoutedCommand();
		public static RoutedCommand OpenCommand = new RoutedCommand();
		public static RoutedCommand RemoveCommand = new RoutedCommand();
		public static RoutedCommand SaveCommand = new RoutedCommand();
		public static RoutedCommand DiscardCommand = new RoutedCommand();
		public static RoutedCommand EmulateStrategyCommand = new RoutedCommand();
		public static RoutedCommand ExecuteStrategyCommand = new RoutedCommand();
		public static RoutedCommand ConnectorSettingsCommand = new RoutedCommand();
		public static RoutedCommand ConnectDisconnectCommand = new RoutedCommand();
		public static RoutedCommand RefreshCompositionCommand = new RoutedCommand();
		public static RoutedCommand OpenMarketDataSettingsCommand = new RoutedCommand();
		public static RoutedCommand HelpCommand = new RoutedCommand();
		public static RoutedCommand AboutCommand = new RoutedCommand();
		public static RoutedCommand TargetPlatformCommand = new RoutedCommand();
		public static RoutedCommand ResetSettingsCommand = new RoutedCommand();
		public static RoutedCommand AddNewSecurityCommand = new RoutedCommand();

		private readonly string _settingsFile;

		private StrategiesRegistry _strategiesRegistry;
		private Connector _connector;
		private LayoutManager _layoutManager;
		private MarketDataSettingsCache _marketDataSettingsCache;
		private EmulationSettings _emulationSettings;

		private bool _isReseting;

		private object ActiveLayoutContent => (DockingManager.ActiveLayoutItem as DocumentPanel)?.Content;

		private object ActiveDockContent => (DockingManager.ActiveDockItem as DocumentPanel)?.Content;

		public MainWindow()
		{
			ConfigManager.RegisterService<IStudioCommandService>(new StudioCommandService());
			ConfigManager.RegisterService<IPersistableService>(new PersistableService());

			InitializeComponent();
			Title = TypeHelper.ApplicationNameWithVersion;

			Directory.CreateDirectory(BaseApplication.AppDataPath);
			_settingsFile = Path.Combine(BaseApplication.AppDataPath, "settings.xml");

			InitializeLogManager();
			InitializeLayoutManager();
			InitializeDataSource();
			InitializeMarketDataSettingsCache();
			InitializeEmulationSettings();
			InitializeCommands();
			InitializeConnector();
			InitializeStrategiesRegistry();

			SolutionExplorer.Compositions = _strategiesRegistry.Compositions;
			SolutionExplorer.Strategies = _strategiesRegistry.Strategies;
			//EmulationSecurityEditor.SecurityProvider = _connector;
        }

		private static void InitializeDataSource()
		{
			((EntityRegistry)ConfigManager.GetService<IEntityRegistry>()).FirstTimeInit(Properties.Resources.StockSharp);
		}

		private void InitializeCommands()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();

			cmdSvc.Register<OpenMarketDataSettingsCommand>(this, true, cmd => OpenMarketDataPanel(cmd.Settings));

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

			cmdSvc.Register<SetDefaultEmulationSettingsCommand>(this, false, cmd =>
			{
				_emulationSettings.Load(cmd.Settings.Save());
				_layoutManager.FlushSettings();
			});
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

			_marketDataSettingsCache.Changed += _layoutManager.FlushSettings;

			ConfigManager.RegisterService(_marketDataSettingsCache);
		}

		private void InitializeEmulationSettings()
		{
			_emulationSettings = new EmulationSettings
			{
				MarketDataSettings = _marketDataSettingsCache.Settings.FirstOrDefault()
			};
		}

		private void InitializeLogManager()
		{
			var logsPath = Path.Combine(BaseApplication.AppDataPath, "Logs");
			var logManager = new LogManager();

			logManager.Listeners.Add(new FileLogListener
			{
				Append = true,
				LogDirectory = logsPath,
				MaxLength = 1024 * 1024 * 100 /* 100mb */,
				MaxCount = 10,
				SeparateByDates = SeparateByDateModes.SubDirectories,
			});
			logManager.Listeners.Add(new GuiLogListener(Monitor));
			ConfigManager.RegisterService(logManager);
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
			_layoutManager.Changed += SaveSettings;
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
			LoadSettings();

			_connector.StorageAdapter.Load();
		}

		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			foreach (var control in _layoutManager.DockingControls)
				control.CanClose();

			_layoutManager.Dispose();
		}

		private void SolutionExplorer_OnOpen(CompositionItem element)
		{
			OpenComposition(element);
		}

		private void DockingManager_OnLayoutItemActivated(object sender, LayoutItemActivatedEventArgs ea)
		{
			DockItemActivated(ActiveLayoutContent);
		}

		private void DockingManager_OnDockItemActivated(object sender, DockItemActivatedEventArgs ea)
		{
			DockItemActivated(ActiveDockContent);
		}

		private void DockingManager_OnDockItemClosed(object sender, DockItemClosedEventArgs e)
		{
			DockItemActivated(ActiveDockContent);
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

		private void AddCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var item = e.OriginalSource as TreeViewItem;

			if (item != null)
			{
				var solutionExplorerItem = item.Header as SolutionExplorerItem;
				e.CanExecute = solutionExplorerItem?.Parent == null;
			}
			else
				e.CanExecute = true;
		}

		private void AddCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var type = (CompositionType)e.Parameter;

			var element = new CompositionDiagramElement
			{
				Name = "New " + type.ToString().ToLower()
			};
			var item = new CompositionItem(type, element);

			_strategiesRegistry.Save(item);

			OpenComposition(item);
		}

		private void OpenCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.Parameter is CompositionItem;
		}

		private void OpenCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			OpenComposition((CompositionItem)e.Parameter);
		}

		private void RemoveCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.Parameter is CompositionItem;
		}

		private void RemoveCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CompositionItem)e.Parameter;

			var res = new MessageBoxBuilder()
				.Owner(this)
				.Caption(Title)
				.Text(LocalizedStrings.Str2884Params.Put(item.Element.Name))
				.Button(MessageBoxButton.YesNo)
				.Icon(MessageBoxImage.Question)
				.Show();

			if (res != MessageBoxResult.Yes)
				return;

			var control = _layoutManager
				.DockingControls
				.OfType<DiagramEditorControl>()
				.FirstOrDefault(c => c.Key.CompareIgnoreCase(item.Key));

			if (control != null)
			{
				control.ResetIsChanged();
				_layoutManager.CloseDocumentWindow(control);
			}

			_strategiesRegistry.Remove(item);
		}

		private void SaveCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var diagramEditor = ActiveDockContent as DiagramEditorControl;
			e.CanExecute = diagramEditor != null && diagramEditor.IsChanged;
		}

		private void SaveCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var diagramEditor = (DiagramEditorControl)ActiveDockContent;
			var item = diagramEditor.Composition;

			_strategiesRegistry.Save(item);

			diagramEditor.ResetIsChanged();
		}

		private void DiscardCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var diagramEditor = ActiveDockContent as DiagramEditorControl;
			e.CanExecute = diagramEditor != null && diagramEditor.IsChanged;
		}

		private void DiscardCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var diagramEditor = (DiagramEditorControl)ActiveDockContent;
			var composition = diagramEditor.Composition;

			_strategiesRegistry.Discard(composition);

			diagramEditor.Composition = null;
			diagramEditor.Composition = composition;
		}

		private void EmulateStrategyCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var item = e.Parameter as CompositionItem;
			e.CanExecute = item != null && item.Type == CompositionType.Strategy;
		}

		private void EmulateStrategyCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			OpenEmulation((CompositionItem)e.Parameter);
		}

		private void ExecuteStrategyCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var item = e.Parameter as CompositionItem;
			e.CanExecute = item != null && item.Type == CompositionType.Strategy;
		}

		private void ExecuteStrategyCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			OpenLive((CompositionItem)e.Parameter);
		}

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

		private void RefreshCompositionCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ActiveDockContent is DiagramEditorControl;
		}

		private void RefreshCompositionCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var diagramEditor = (DiagramEditorControl)ActiveDockContent;
			var composition = diagramEditor.Composition;

			_strategiesRegistry.Reload(composition);

			diagramEditor.Composition = null;
			diagramEditor.Composition = composition;
		}

		private void OpenMarketDataSettingsCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void OpenMarketDataSettingsCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			OpenMarketDataPanel(_marketDataSettingsCache.Settings.FirstOrDefault(s => s.Id != Guid.Empty));
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

			_isReseting = true;

			ConfigManager.GetService<LogManager>().Dispose();
			Directory.Delete(BaseApplication.AppDataPath, true);

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

			var content = new DiagramEditorControl
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
			if (!File.Exists(_settingsFile))
				return;

			CultureInfo
				.InvariantCulture
				.DoInCulture(() =>
				{
					var settings = new XmlSerializer<SettingsStorage>().Deserialize(_settingsFile);

					settings.TryLoadSettings<SettingsStorage>("MarketDataSettingsCache", s => _marketDataSettingsCache.Load(s));
					settings.TryLoadSettings<SettingsStorage>("EmulationSettings", s => _emulationSettings.Load(s));
					settings.TryLoadSettings<SettingsStorage>("Layout", s => _layoutManager.Load(s));
					settings.TryLoadSettings<SettingsStorage>("Connector", s => _connector.Load(s));
					settings.TryLoadSettings<string>("ThemeName", s => ThemeManager.ApplicationThemeName = s);
				});
		}

		private void SaveSettings()
		{
			if (_isReseting)
				return;

			CultureInfo
				.InvariantCulture
				.DoInCulture(() =>
				{
					var settings = new SettingsStorage();

					settings.SetValue("MarketDataSettingsCache", ((IPersistable)_marketDataSettingsCache).Save());
					settings.SetValue("EmulationSettings", _emulationSettings.Save());
					settings.SetValue("Layout", _layoutManager.Save());
					settings.SetValue("Connector", _connector.Save());
					settings.SetValue("ThemeName", ThemeManager.ApplicationThemeName);

					new XmlSerializer<SettingsStorage>().Serialize(settings, _settingsFile);
				});
		}

		private bool ConfigureConnector()
		{
			var result = _connector.Configure(this);

			if (!result)
				return false;

			_layoutManager.FlushSettings();

			return true;
		}

		private void DockItemActivated(object control)
		{
			if (control == null)
			{
				RibbonEmulationTab.DataContext = null;
				RibbonLiveTab.DataContext = null;
				RibbonDesignerTab.DataContext = null;
				Ribbon.SelectedPage = RibbonCommonTab;

				return;
			}

			control
				.DoIf<object, DiagramEditorControl>(editor =>
				{
					RibbonEmulationTab.DataContext = null;
					RibbonLiveTab.DataContext = null;
					RibbonDesignerTab.DataContext = editor.Composition;
					Ribbon.SelectedPage = RibbonDesignerTab;
				});

			control
				.DoIf<object, EmulationStrategyControl>(editor =>
				{
					RibbonDesignerTab.DataContext = null;
					RibbonLiveTab.DataContext = null;
					RibbonEmulationTab.DataContext = editor;
					Ribbon.SelectedPage = RibbonEmulationTab;
				});

			control
				.DoIf<object, LiveStrategyControl>(editor =>
				{
					RibbonEmulationTab.DataContext = null;
					RibbonDesignerTab.DataContext = null;
					RibbonLiveTab.DataContext = editor;
					Ribbon.SelectedPage = RibbonLiveTab;
				});
		}
	}
}