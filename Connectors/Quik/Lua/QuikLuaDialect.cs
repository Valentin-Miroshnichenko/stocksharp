namespace StockSharp.Quik.Lua
{
	using System;
	using System.IO;
	using System.Security;
	using System.Text;

	using Ecng.Common;

	using StockSharp.Fix.Dialects;
	using StockSharp.Fix.Native;
	using StockSharp.Messages;

	class QuikLuaDialect : DefaultDialect
	{
		public QuikLuaDialect(string senderCompId, string targetCompId, Stream stream, Encoding encoding, IncrementalIdGenerator idGenerator, TimeSpan heartbeatInterval, bool isResetCounter, string login, SecureString password, Func<OrderCondition> createOrderCondition)
			: base(senderCompId, targetCompId, stream, encoding, idGenerator, heartbeatInterval, isResetCounter, login, password, TimeHelper.Moscow, createOrderCondition)
		{
		}

		/// <summary>
		/// �������� ������ �� ������� ������.
		/// </summary>
		/// <param name="writer">�������� FIX ������.</param>
		/// <param name="regMsg">���������, ���������� ���������� ��� ����������� ������.</param>
		protected override void WriteOrderCondition(IFixWriter writer, OrderRegisterMessage regMsg)
		{
			writer.WriteOrderCondition((QuikOrderCondition)regMsg.Condition, TimeStampFormat);
		}

		/// <summary>
		/// ��������� ������� ����������� ������ <see cref="OrderRegisterMessage.Condition"/>.
		/// </summary>
		/// <param name="reader">�������� ������.</param>
		/// <param name="tag">���.</param>
		/// <param name="getCondition">�������, ������������ ������� ������.</param>
		/// <returns>������� �� ���������� ������.</returns>
		protected override bool ReadOrderCondition(IFixReader reader, FixTags tag, Func<OrderCondition> getCondition)
		{
			return reader.ReadOrderCondition(tag, TimeZone, TimeStampFormat, () => (QuikOrderCondition)getCondition());
		}
	}
}