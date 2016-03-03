#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleDiagram.Layout.SampleDiagramPublic
File: LayoutManager.cs
Created: 2015, 12, 14, 1:43 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Designer.Layout
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;

	using DevExpress.Xpf.Core.Serialization;
	using DevExpress.Xpf.Docking;
	using DevExpress.Xpf.Docking.Base;
	using DevExpress.Xpf.Layout.Core;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.Serialization;
	using Ecng.Xaml;

	using StockSharp.Localization;
	using StockSharp.Logging;
	using StockSharp.Studio.Controls;
	using StockSharp.Studio.Core;

	public sealed class LayoutManager : BaseLogReceiver
	{
		private readonly DocumentGroup _documentGroup;
		private readonly Dictionary<string, LayoutPanel> _panels = new Dictionary<string, LayoutPanel>();
		private readonly List<IStudioControl> _controls = new List<IStudioControl>();

		private readonly SynchronizedDictionary<IStudioControl, SettingsStorage> _dockingControlSettings = new SynchronizedDictionary<IStudioControl, SettingsStorage>();
		private readonly SynchronizedSet<IStudioControl> _changedControls = new CachedSynchronizedSet<IStudioControl>();

		private bool _isLayoutChanged;
		private string _layout;

		public DockLayoutManager DockingManager { get; }

		public IEnumerable<IStudioControl> DockingControls => _controls.ToArray();

		public event Action Changed; 

		public LayoutManager(DockLayoutManager dockingManager, DocumentGroup documentGroup = null)
		{
			if (dockingManager == null)
				throw new ArgumentNullException(nameof(dockingManager));

			_documentGroup = documentGroup;

			DockingManager = dockingManager;
			DXSerializer.AddAllowPropertyHandler(DockingManager, (s, e) =>
			{
				if (e.DependencyProperty == BaseLayoutItem.CaptionProperty)
					e.Allow = false;
			});

			DockingManager.DockItemClosing += OnDockingManagerDockItemClosing;
			DockingManager.DockItemClosed += OnDockingManagerDockItemClosed;
			DockingManager.DockItemHidden += DockingManager_DockItemHidden;
			DockingManager.DockItemRestored += DockingManager_DockItemRestored;
			DockingManager.DockItemExpanded += DockingManager_DockItemExpanded;
			DockingManager.DockItemActivated += DockingManager_DockItemActivated;
			DockingManager.DockItemEndDocking += DockingManager_DockItemEndDocking;

			DockingManager.LayoutItemSizeChanged += DockingManager_LayoutItemSizeChanged;
		}

		public void OpenToolWindow(IStudioControl content, bool canClose = true)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));

			var panel = _panels.TryGetValue(content.Key);

			if (panel == null)
			{
				panel = DockingManager.DockController.AddPanel(DockType.Left);
				panel.Name = "_" + content.Key.Replace("-", "_");
				panel.Content = content;
				panel.ShowCloseButton = canClose;

				panel.SetBindings(BaseLayoutItem.CaptionProperty, content, "Title");

				_panels.Add(content.Key, panel);
				_controls.Add(content);

				MarkControlChanged(content);
			}

			DockingManager.ActiveLayoutItem = panel;
		}

		public void OpenDocumentWindow(IStudioControl content, bool canClose = true)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));

			var document = _panels.TryGetValue(content.Key);

			if (document == null)
			{
				document = DockingManager.DockController.AddDocumentPanel(_documentGroup);
				document.Name = "_" + content.Key.Replace("-", "_");
				document.Content = content;
				document.ShowCloseButton = canClose;

				document.SetBindings(BaseLayoutItem.CaptionProperty, content, "Title");

				_panels.Add(content.Key, document);
				_controls.Add(content);

				MarkControlChanged(content);
			}

			DockingManager.ActiveLayoutItem = document;
		}

		public void CloseWindow(IStudioControl content)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));

			var panel = _panels.TryGetValue(content.Key);

			if (panel == null)
				return;

			DockingManager.DockController.Close(panel);
		}

		public override void Load(SettingsStorage storage)
		{
			if (storage == null)
				throw new ArgumentNullException(nameof(storage));

			_panels.Clear();
			_changedControls.Clear();
			_dockingControlSettings.Clear();

			var controls = storage.GetValue<SettingsStorage[]>("Controls");

			foreach (var settings in controls)
			{
				try
				{
					var control = LoadBaseStudioControl(settings);
					var isToolWindow = settings.GetValue("IsToolWindow", true);

					_dockingControlSettings.Add(control, settings);

					if (isToolWindow)
						OpenToolWindow(control);
					else
						OpenDocumentWindow(control);
				}
				catch (Exception excp)
				{
					this.AddErrorLog(excp);
				}
			}

			_layout = storage.GetValue<string>("Layout");

			if (!_layout.IsEmpty())
				LoadLayout(_layout);
		}

		public override void Save(SettingsStorage storage)
		{
			if (storage == null)
				throw new ArgumentNullException(nameof(storage));

			var items = _changedControls.CopyAndClear();
			var isLayoutChanged = _isLayoutChanged;

			_isLayoutChanged = false;

			if (items.Length > 0 || isLayoutChanged)
				Save(items, isLayoutChanged);

			storage.SetValue("Controls", _dockingControlSettings.SyncGet(c => c.Select(p => p.Value).ToArray()));
			storage.SetValue("Layout", _layout);
		}

		public void LoadLayout(string settings)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			try
			{
				var titles = DockingManager
					.GetItems()
					.OfType<LayoutPanel>()
					.Where(c => !c.Name.IsEmpty())
					.ToDictionary(c => c.Name, c => (string)c.Caption);

				var data = Encoding.UTF8.GetBytes(settings);

                using (var stream = new MemoryStream(data))
					DockingManager.RestoreLayoutFromStream(stream);

				var items = DockingManager
					.GetItems()
					.OfType<LayoutPanel>()
					.ToArray();

				foreach (var content in items)
				{
					_panels[content.Name] = content;

                    //content.DoIf<LayoutPanel, DocumentPanel>(d => _panels[d.Name] = d);
					//content.DoIf<ContentItem, DocumentPanel>(d => _anchorables[d.ContentId] = d);

					if (!(content.Content is BaseStudioControl))
					{
						var title = titles.TryGetValue(content.Name);

						if (!title.IsEmpty())
							content.Caption = title;
					}
					else
						content.SetBindings(BaseLayoutItem.CaptionProperty, content.Content, "Title");
				}
			}
			catch (Exception excp)
			{
				this.AddErrorLog(excp, LocalizedStrings.Str3649);
			}
		}

		public string SaveLayout()
		{
			var layout = string.Empty;

			try
			{
				using (var stream = new MemoryStream())
				{
					DockingManager.SaveLayoutToStream(stream);
					layout = Encoding.UTF8.GetString(stream.ToArray());
				}
			}
			catch (Exception excp)
			{
				this.AddErrorLog(excp, LocalizedStrings.Str3649);
			}

			return layout;
		}

		public void MarkControlChanged(IStudioControl control)
		{
			_changedControls.Add(control);
			Changed.SafeInvoke();
		}

		private void OnDockingManagerDockItemClosing(object sender, ItemCancelEventArgs e)
		{
			var control = ((LayoutPanel)e.Item).Content as BaseStudioControl;

			if (control == null)
				return;

			e.Cancel = !control.CanClose();
		}

		private void OnDockingManagerDockItemClosed(object sender, DockItemClosedEventArgs e)
		{
			var panel = (LayoutPanel)e.Item;
            var control = panel.Content as BaseStudioControl;

			if (control == null)
				return;

			_panels.RemoveWhere(p => Equals(p.Value, panel));
			_controls.Remove(control);

			_isLayoutChanged = true;

			_changedControls.Remove(control);
			_dockingControlSettings.Remove(control);

			Changed.SafeInvoke();
		}

		private void DockingManager_DockItemEndDocking(object sender, DockItemDockingEventArgs e)
		{
			OnDickingChanged();
		}

		private void DockingManager_DockItemActivated(object sender, DockItemActivatedEventArgs ea)
		{
			OnDickingChanged();
		}

		private void DockingManager_DockItemExpanded(object sender, DockItemExpandedEventArgs e)
		{
			OnDickingChanged();
		}

		private void DockingManager_DockItemRestored(object sender, ItemEventArgs e)
		{
			OnDickingChanged();
		}

		private void DockingManager_DockItemHidden(object sender, ItemEventArgs e)
		{
			OnDickingChanged();
		}

		private void DockingManager_LayoutItemSizeChanged(object sender, LayoutItemSizeChangedEventArgs e)
		{
			OnDickingChanged();
		}

		private void OnDickingChanged()
		{
			_isLayoutChanged = true;
			Changed.SafeInvoke();
		}

		private void Save(IEnumerable<IStudioControl> items, bool isLayoutChanged)
		{
			CultureInfo.InvariantCulture.DoInCulture(() =>
			{
				foreach (var control in items)
				{
					var isDocumentPanel = _panels.TryGetValue(control.Key) is DocumentPanel;

					var storage = new SettingsStorage();
					storage.SetValue("ControlType", control.GetType().GetTypeName(false));
					storage.SetValue("IsToolWindow", !isDocumentPanel);
					control.Save(storage);

					_dockingControlSettings[control] = storage;
				}

				if (isLayoutChanged)
					_layout = SaveLayout();
			});
		}

		private static BaseStudioControl LoadBaseStudioControl(SettingsStorage settings)
		{
			var type = settings.GetValue<Type>("ControlType");
			var control = (BaseStudioControl)Activator.CreateInstance(type);

			control.Load(settings);

			return control;
		}
	}
}
