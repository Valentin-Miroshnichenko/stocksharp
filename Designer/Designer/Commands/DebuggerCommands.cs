namespace StockSharp.Designer.Commands
{
	using StockSharp.Studio.Core.Commands;

	class DebuggerStateCommand : BaseStudioCommand
	{
		public bool IsEnabled { get; private set; }

		public DebuggerStateCommand(bool isEnabled)
		{
			IsEnabled = isEnabled;
		}
	}

	class DebuggerAddBreakpointCommand : BaseStudioCommand
	{
	}

	class DebuggerRemoveBreakpointCommand : BaseStudioCommand
	{
	}

	class DebuggerStepNextCommand : BaseStudioCommand
	{
	}

	class DebuggerStepIntoCommand : BaseStudioCommand
	{
	}

	class DebuggerStepOutCommand : BaseStudioCommand
	{
	}

	class DebuggerContinueCommand : BaseStudioCommand
	{
	}
}