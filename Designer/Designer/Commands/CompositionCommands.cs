namespace StockSharp.Designer.Commands
{
	using System;

	using StockSharp.Studio.Core.Commands;

	class AddCompositionCommand : BaseStudioCommand
	{
		public CompositionType Type { get; private set; }

		public AddCompositionCommand(CompositionType type)
		{
			Type = type;
		}
	}

	class OpenCompositionCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public OpenCompositionCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}

	class RemoveCompositionCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public RemoveCompositionCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}

	class SaveCompositionCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public SaveCompositionCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}

	class DiscardCompositionCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public DiscardCompositionCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}

	class RefreshCompositionCommand : BaseStudioCommand
	{
		public CompositionItem Element { get; private set; }

		public RefreshCompositionCommand(CompositionItem element)
		{
			if (element == null)
				throw new ArgumentNullException(nameof(element));

			Element = element;
		}
	}
}
