namespace StockSharp.Designer.Configuration
{
	using System.Configuration;

	using StockSharp.Configuration;

	class DesignerSection : StockSharpSection
	{
		private const string _toolControlsKey = "toolControls";

		[ConfigurationProperty(_toolControlsKey, IsDefaultCollection = true)]
		[ConfigurationCollection(typeof(ControlElementCollection), AddItemName = "control", ClearItemsName = "clear", RemoveItemName = "remove")]
		public ControlElementCollection ToolControls => (ControlElementCollection)base[_toolControlsKey];

		private const string _strategyControlsKey = "strategyControls";

		[ConfigurationProperty(_strategyControlsKey, IsDefaultCollection = true)]
		[ConfigurationCollection(typeof(ControlElementCollection), AddItemName = "control", ClearItemsName = "clear", RemoveItemName = "remove")]
		public ControlElementCollection StrategyControls => (ControlElementCollection)base[_strategyControlsKey];
	}
}
