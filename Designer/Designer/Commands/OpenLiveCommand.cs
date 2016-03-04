namespace StockSharp.Designer.Commands
{
	using System;

	using StockSharp.Studio.Core.Commands;

	class OpenLiveCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public OpenLiveCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}
}