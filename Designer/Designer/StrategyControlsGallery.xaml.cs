namespace StockSharp.Designer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Windows;

	using DevExpress.Xpf.Bars;

	using Ecng.Common;
	using Ecng.Xaml;

	using Ookii.Dialogs.Wpf;

	using StockSharp.Localization;
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

			ControlTypes = AppConfig.Instance.StrategyControls.GetControlTypes();
		}

		private void Gallery_OnItemClick(object sender, GalleryItemEventArgs e)
		{
			var type = e.Item.DataContext as ControlType;
			var ctrl = DataContext as StrategyPanel;

			if (type == null || ctrl == null)
				return;

			new OpenWindowCommand(Guid.NewGuid().To<string>(), type.Type, true).SyncProcess(ctrl.Strategy);
		}

		private void SaveLayout_OnItemClick(object sender, ItemClickEventArgs e)
		{
			var dlg = new VistaSaveFileDialog
			{
				Filter = LocalizedStrings.Str3584,
				DefaultExt = "xml",
				RestoreDirectory = true
			};

			if (dlg.ShowDialog(Application.Current.GetActiveOrMainWindow()) != true)
				return;

			var cmd = new SaveLayoutCommand();

			cmd.SyncProcess(((StrategyPanel)DataContext).Strategy);

			if (!cmd.Layout.IsEmpty())
				File.WriteAllText(dlg.FileName, cmd.Layout);
		}

		private void LoadLayout_OnItemClick(object sender, ItemClickEventArgs e)
		{
			var dlg = new VistaOpenFileDialog
			{
				Filter = LocalizedStrings.Str3584,
				CheckFileExists = true,
				RestoreDirectory = true
			};

			if (dlg.ShowDialog(Application.Current.GetActiveOrMainWindow()) != true)
				return;

			var data = File.ReadAllText(dlg.FileName);

			new LoadLayoutCommand(data).SyncProcess(((StrategyPanel)DataContext).Strategy);
		}
	}
}
