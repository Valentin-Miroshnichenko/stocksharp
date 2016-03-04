namespace StockSharp.Designer.Commands
{
	using System;

	using StockSharp.Studio.Core.Commands;

	class OpenBacktestingCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public OpenBacktestingCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}
}