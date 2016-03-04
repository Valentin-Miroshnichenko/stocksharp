#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Studio.Controls.ControlsPublic
File: LogManagerPanel.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Studio.Controls
{
	using System;
	using System.Windows.Controls;
	using System.Runtime.InteropServices;

	using Ecng.ComponentModel;
	using Ecng.Configuration;
	using Ecng.Serialization;

	using StockSharp.Logging;
	using StockSharp.Studio.Core;
	using StockSharp.Studio.Core.Commands;
	using StockSharp.Xaml;
	using StockSharp.Localization;

	[DisplayNameLoc(LocalizedStrings.Str3237Key)]
	[Guid("F97DCB8B-2104-4DF3-B6C5-CBB2B8B3B704")]
	[Icon("images/log_24x24.png")]
	[DockingWindowType(true)]
	public class LogManagerPanel : BaseStudioControl
	{
		private readonly Monitor _monitor = new Monitor();
		private readonly GuiLogListener _listener;

		public LogManagerPanel()
		{
			Content = _monitor;
			_listener = new GuiLogListener(_monitor);

			// загрузка контрола происходит только при открытии окна актипро
			// если окно свернуто, то событие загрузки не вызывается.
			ConfigManager.GetService<LogManager>().Listeners.Add(_listener);
			//Loaded += OnLoaded;
		}

		//private void OnLoaded(object sender, RoutedEventArgs e)
		//{
		//	Loaded -= OnLoaded;
		//	new AddLogListenerCommand(_listener).Process(this);
		//}

		public override void Load(SettingsStorage storage)
		{
			_monitor.Load(storage);
		}

		public override void Save(SettingsStorage storage)
		{
			_monitor.Save(storage);
		}

		public override void Dispose()
		{
			new RemoveLogListenerCommand(_listener).Process(this);
		}
	}
}