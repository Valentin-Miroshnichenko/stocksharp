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
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using System.Windows.Media.Imaging;

	using DevExpress.Xpf.Bars;
	using DevExpress.Xpf.Core.Serialization;
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

		public static void AddToolControl(this RibbonPageGroup page, ControlType controlType, object sender)
		{
			if (page == null)
				throw new ArgumentNullException(nameof(page));

			if (controlType == null)
				throw new ArgumentNullException(nameof(controlType));

			if (sender == null)
				throw new ArgumentNullException(nameof(sender));

			var type = controlType.Type;
			var id = type.GUID.ToString();
			var isToolWindow = controlType.IsToolWindow;

			var mi = new BarButtonItem
			{
				Content = controlType.Name,
				ToolTip = controlType.Description,
				LargeGlyph = controlType.Icon == null ? null : new BitmapImage(controlType.Icon)
			};
			mi.ItemClick += (s, e) => new OpenWindowCommand(id, type, isToolWindow).Process(sender);

			page.Items.Add(mi);
		}

		public static string SaveDevExpressControl(this DependencyObject obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			using (var stream = new MemoryStream())
			{
				DXSerializer.Serialize(obj, stream, "Designer", null);
				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}

		public static void LoadDevExpressControl(this DependencyObject obj, string settings)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			var data = Encoding.UTF8.GetBytes(settings);

			using (var stream = new MemoryStream(data))
			{
				DXSerializer.Deserialize(obj, stream, "Designer", null);
			}
		}

		public static IEnumerable<ControlType> GetControlTypes(this IEnumerable<Type> types)
		{
			return types
				.Select(type => new ControlType(type))
				.ToArray();
		}
	}
}