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
	using StockSharp.Studio.Controls;

	public class AppConfig
	{
		private static AppConfig _instance;

		public static AppConfig Instance => _instance ?? (_instance = new AppConfig());

		private readonly CachedSynchronizedList<Type> _strategyControls = new CachedSynchronizedList<Type>();
		private readonly CachedSynchronizedList<Type> _toolControls = new CachedSynchronizedList<Type>();

		public IEnumerable<Type> StrategyControls => _strategyControls.Cache;

		public IEnumerable<Type> ToolControls => _toolControls.Cache;

		private AppConfig()
		{
			var section = ConfigManager.GetSection<DesignerSection>();

			SafeAdd<ControlElement>(section.StrategyControls, elem => _strategyControls.Add(elem.Type.To<Type>()));
			SafeAdd<ControlElement>(section.ToolControls, elem => _toolControls.Add(elem.Type.To<Type>()));
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
	}

	public class ControlType
	{
		public Type Type { get; }

		public string Name { get; }

		public string Description { get; }

		public Uri Icon { get; }

		public bool IsToolWindow { get; }

		public ControlType(Type type)
			: this(type, type.GetDisplayName(), type.GetDescription(), type.GetIconUrl())
		{
			var attr = type.GetAttribute<DockingWindowTypeAttribute>();
			IsToolWindow = attr != null && attr.IsToolWindow;
		}

		public ControlType(Type type, string name, string description, Uri icon)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (name == null)
				throw new ArgumentNullException(nameof(name));

			Type = type;
			Name = name;
			Description = description;
			Icon = icon;
		}
	}
}