namespace StockSharp.Designer
{
	using System;
	using System.Collections.Generic;
	using System.Windows;

	using DevExpress.Xpf.Bars;

	using Ecng.Common;
	using Ecng.Xaml;

	using StockSharp.Studio.Core.Commands;

	public partial class StrategyControlsGallery
	{
		public static readonly DependencyProperty ControlTypesProperty = DependencyProperty.Register("ControlTypes", typeof(IEnumerable<ControlType>), typeof(StrategyControlsGallery));

		public IEnumerable<ControlType> ControlTypes
		{
			get { return (IEnumerable<ControlType>)GetValue(ControlTypesProperty); }
			set { SetValue(ControlTypesProperty, value); }
		}

		public StrategyControlsGallery()
		{
			InitializeComponent();

			if (this.IsDesignMode())
				return;

			ControlTypes = AppConfig.Instance.GetControlTypes();
		}

		private void Gallery_OnItemClick(object sender, GalleryItemEventArgs e)
		{
			var type = e.Item.DataContext as ControlType;
			var ctrl = DataContext as StrategyControl;

			if (type == null || ctrl == null)
				return;

			new OpenWindowCommand(Guid.NewGuid().To<string>(), type.Item1, true).SyncProcess(ctrl.Strategy);
		}
	}
}
