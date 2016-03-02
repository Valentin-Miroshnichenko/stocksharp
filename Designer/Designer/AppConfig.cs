namespace StockSharp.Designer
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.ComponentModel;
	using Ecng.Configuration;
	using Ecng.Xaml;

	using StockSharp.Designer.Configuration;
	using StockSharp.Logging;

	public class AppConfig
	{
		private static AppConfig _instance;

		public static AppConfig Instance => _instance ?? (_instance = new AppConfig());

		private readonly CachedSynchronizedList<Type> _strategyControls = new CachedSynchronizedList<Type>();

		public IEnumerable<Type> StrategyControls => _strategyControls.Cache;

		private AppConfig()
		{
			var section = ConfigManager.GetSection<DesignerSection>();

			SafeAdd<ControlElement>(section.StrategyControls, elem => _strategyControls.Add(elem.Type.To<Type>()));
		}

		private static void SafeAdd<T1>(IEnumerable from, Action<T1> action)
		{
			foreach (T1 item in from)
			{
				try
				{
					action(item);
				}
				catch (Exception e)
				{
					e.LogError();
				}
			}
		}

		public IEnumerable<ControlType> GetControlTypes()
		{
			return StrategyControls
				.Select(type => new ControlType(type,
					type.GetDisplayName(),
					type.GetDescription(),
					type.GetIconUrl()))
				.ToArray();
		}
	}

	public class ControlType : Tuple<Type, string, string, Uri>
	{
		public ControlType(Type item1, string item2, string item3, Uri item4)
			: base(item1, item2, item3, item4)
		{
		}
	}
}