#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleDiagram.SampleDiagramPublic
File: Extensions.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Designer
{
	using System;
	using System.Windows.Media.Imaging;

	using DevExpress.Xpf.Bars;
	using DevExpress.Xpf.Ribbon;

	using Ecng.ComponentModel;
	using Ecng.Xaml;

	using StockSharp.Studio.Core.Commands;
	using StockSharp.Xaml.Diagram;

	static class Extensions
	{
		public static string GetFileName(this CompositionDiagramElement element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			return element.TypeId.ToString().Replace("-", "_") + ".xml";
		}

		public static void AddToolControl(this RibbonPageGroup page, Type type, object sender)
		{
			var id = type.GUID.ToString();

			var mi = CreateRibbonButton(type);
			mi.ItemClick += (s, e) => new OpenWindowCommand(id, type, false).Process(sender);

			page.Items.Add(mi);
		}

		private static BarButtonItem CreateRibbonButton(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var iconUrl = type.GetIconUrl();

			return new BarButtonItem
			{
				Content = type.GetDisplayName(),
				ToolTip = type.GetDescription(),
				LargeGlyph = iconUrl == null ? null : new BitmapImage(iconUrl)
			};
		}
	}
}