#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleDiagram.SampleDiagramPublic
File: DiagramDebuggerControl.xaml.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Designer
{
	using System;
	using System.ComponentModel;
	using System.Windows;

	using Ecng.Common;
	using Ecng.ComponentModel;
	using Ecng.Configuration;
	using Ecng.Serialization;
	using Ecng.Xaml;

	using StockSharp.Algo;
	using StockSharp.Algo.Strategies;
	using StockSharp.Designer.Commands;
	using StockSharp.Designer.Layout;
	using StockSharp.Localization;
	using StockSharp.Studio.Core.Commands;
	using StockSharp.Xaml.Diagram;

	[DisplayNameLoc(LocalizedStrings.Str3230Key)]
	[DescriptionLoc(LocalizedStrings.Str3231Key)]
	[Icon("images/bug_24x24.png")]
	public partial class DiagramDebuggerControl
	{
		private readonly LayoutManager _layoutManager;

		#region Strategy

		public static readonly DependencyProperty StrategyProperty = DependencyProperty.Register(nameof(Strategy), typeof(DiagramStrategy), typeof(DiagramDebuggerControl),
			new PropertyMetadata(null, OnStrategyPropertyChanged));

		private SettingsStorage _debuggerSettings;

		private static void OnStrategyPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
		{
			((DiagramDebuggerControl)sender).OnStrategyPropertyChanged((DiagramStrategy)args.NewValue);
		}

		public DiagramStrategy Strategy
		{
			get { return (DiagramStrategy)GetValue(StrategyProperty); }
			set { SetValue(StrategyProperty, value); }
		}

		#endregion

		public DiagramDebugger Debugger { get; private set; }

		public DiagramDebuggerControl()
		{
			InitializeCommands();
            InitializeComponent();
			
			_layoutManager = new LayoutManager(DockingManager);

			WhenLoaded(()=> new RequestBindSource(this).SyncProcess(this));
		}

		private void InitializeCommands()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();
			cmdSvc.Register<BindStrategyCommand>(this, true, cmd =>
			{
				if (!cmd.CheckControl(this))
					return;

				Strategy = cmd.Source as DiagramStrategy;
			});
			cmdSvc.Register<DebuggerStateCommand>(this, true, cmd => Debugger.IsEnabled = cmd.IsEnabled);

			cmdSvc.Register<DebuggerAddBreakpointCommand>(this, true, 
				cmd =>
				{
					Debugger.AddBreak(DiagramEditor.SelectedElement.SelectedSocket);
					RaiseChangedCommand();
				},
				cmd => SafeCheckDebugger((d, s) => !d.IsBreak(s)));

			cmdSvc.Register<DebuggerRemoveBreakpointCommand>(this, true,
				cmd =>
				{
					Debugger.RemoveBreak(DiagramEditor.SelectedElement.SelectedSocket);
					RaiseChangedCommand();
				},
				cmd => SafeCheckDebugger((d, s) => d.IsBreak(s)));

			cmdSvc.Register<DebuggerStepNextCommand>(this, true,
				cmd => Debugger.StepNext(),
				cmd => Debugger != null && Debugger.IsWaiting);

			cmdSvc.Register<DebuggerStepIntoCommand>(this, true,
				obj => Debugger.StepInto(DiagramEditor?.SelectedElement as CompositionDiagramElement),
				obj => (Debugger != null && Debugger.IsWaitingOnInput && Debugger.CanStepInto) || DiagramEditor?.SelectedElement is CompositionDiagramElement);

			cmdSvc.Register<DebuggerStepOutCommand>(this, true,
				obj => Debugger.StepOut(DiagramEditor.Composition),
				obj => Debugger != null && Debugger.CanStepOut);

			cmdSvc.Register<DebuggerContinueCommand>(this, true,
				obj => Debugger.Continue(),
				obj => Debugger != null && Debugger.IsWaiting);
		}

		private void OnDiagramEditorSelectionChanged(DiagramElement element)
		{
			ShowElementProperties(element);
		}

		private void OnDiagramEditorElementDoubleClicked(DiagramElement element)
		{
			var composition = element as CompositionDiagramElement;

			if (composition == null)
				return;

			Debugger.StepInto(composition);
		}

		private void OnStrategyPropertyChanged(DiagramStrategy strategy)
		{
			if (strategy != null)
			{
				strategy.PropertyChanged += OnStrategyPropertyChanged;
				strategy.ProcessStateChanged += OnStrategyProcessStateChanged;

				var composition = strategy.Composition;

				Debugger = new DiagramDebugger(composition);
				Debugger.Break += OnDebuggerBreak;
				Debugger.CompositionChanged += OnDebuggerCompositionChanged;

				SafeLoadDebuggerSettings();

				NoStrategyLabel.Visibility = Visibility.Hidden;
				DiagramEditor.Composition = composition;

				ShowElementProperties(null);
			}
			else
			{
				Debugger = null;

				NoStrategyLabel.Visibility = Visibility.Visible;
				DiagramEditor.Composition = new CompositionDiagramElement { Name = string.Empty };
			}

			DiagramEditor.Composition.IsModifiable = false;
		}

		private void OnStrategyPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			//Changed.SafeInvoke();
		}

		private void OnStrategyProcessStateChanged(Strategy strategy)
		{
			this.GuiAsync(() =>
			{
				if (PropertyGridControl.SelectedObject == strategy)
					PropertyGridControl.IsReadOnly = strategy.ProcessState != ProcessStates.Stopped;
			});
		}

		private void OnDebuggerBreak(DiagramSocket socket)
		{
			this.GuiAsync(() =>
			{
				var element = socket.Parent;

				DiagramEditor.SelectedElement = element;
				ShowElementProperties(element);
			});
		}

		private void OnDebuggerCompositionChanged(CompositionDiagramElement element)
		{
			this.GuiAsync(() => DiagramEditor.Composition = element);
		}

		private void ShowElementProperties(DiagramElement element)
		{
			if (element != null)
			{
				if (PropertyGridControl.SelectedObject == element)
					PropertyGridControl.SelectedObject = null;

				PropertyGridControl.SelectedObject = new DiagramElementParameters(element);
				PropertyGridControl.IsReadOnly = true;
			}
			else
			{
				PropertyGridControl.SelectedObject = Strategy;
				PropertyGridControl.IsReadOnly = Strategy.ProcessState != ProcessStates.Stopped;
			}
		}

		private bool SafeCheckDebugger(Func<DiagramDebugger, DiagramSocket, bool> func)
		{
			return Debugger != null && 
				DiagramEditor.SelectedElement != null &&
				DiagramEditor.SelectedElement.SelectedSocket != null && 
				func(Debugger, DiagramEditor.SelectedElement.SelectedSocket);
		}

		private void SafeLoadDebuggerSettings()
		{
			if (Debugger == null || _debuggerSettings == null)
				return;

			Debugger.Load(_debuggerSettings);
		}

		#region IPersistable

		public override void Load(SettingsStorage storage)
		{
			base.Load(storage);

			_debuggerSettings = storage.GetValue<SettingsStorage>("DebuggerSettings");
			SafeLoadDebuggerSettings();

			var layout = storage.GetValue<string>("Layout");

			if (!layout.IsEmpty())
				_layoutManager.LoadLayout(layout);

			var diagramEditor = storage.GetValue<SettingsStorage>("DiagramEditor");

			if (diagramEditor != null)
				DiagramEditor.Load(diagramEditor);
		}

		public override void Save(SettingsStorage storage)
		{
			base.Save(storage);

			if (Debugger != null)
				storage.SetValue("DebuggerSettings", _debuggerSettings = Debugger.Save());

			storage.SetValue("Layout", _layoutManager.SaveLayout());
			storage.SetValue("DiagramEditor", DiagramEditor.Save());
		}

		#endregion

		public override void Dispose()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();
			cmdSvc.UnRegister<BindStrategyCommand>(this);
			cmdSvc.UnRegister<DebuggerStateCommand>(this);
			cmdSvc.UnRegister<DebuggerAddBreakpointCommand>(this);
			cmdSvc.UnRegister<DebuggerRemoveBreakpointCommand>(this);
			cmdSvc.UnRegister<DebuggerStepNextCommand>(this);
			cmdSvc.UnRegister<DebuggerStepIntoCommand>(this);
			cmdSvc.UnRegister<DebuggerStepOutCommand>(this);
			cmdSvc.UnRegister<DebuggerContinueCommand>(this);
		}
	}
}
