﻿namespace StockSharp.Designer.Configuration
{
	using System.Configuration;

	class ControlElement : ConfigurationElement
	{
		private const string _typeKey = "type";

		[ConfigurationProperty(_typeKey, IsRequired = true, IsKey = true)]
		public string Type
		{
			get { return (string)this[_typeKey]; }
			set { this[_typeKey] = value; }
		}
	}
}