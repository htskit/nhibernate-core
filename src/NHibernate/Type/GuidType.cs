using System;
using System.Data;
using System.Data.Common;
using NHibernate.Engine;
using NHibernate.SqlTypes;

namespace NHibernate.Type
{
	/// <summary>
	/// Maps a <see cref="System.Guid"/> Property 
	/// to a <see cref="DbType.Guid"/> column.
	/// </summary>
	[Serializable]
	public class GuidType : PrimitiveType, IDiscriminatorType
	{
		/// <summary></summary>
		public GuidType() : base(SqlTypeFactory.Guid)
		{
		}

		public override object Get(DbDataReader rs, int index, ISessionImplementor session)
		{
			if (rs.GetFieldType(index) == typeof (Guid))
			{
				return rs.GetGuid(index);
			}

			if (rs.GetFieldType(index) == typeof(byte[]))
			{
				return new Guid((byte[])(rs[index]));
			} 

			return new Guid(Convert.ToString(rs[index]));
		}

		public override object Get(DbDataReader rs, string name, ISessionImplementor session)
		{
			return Get(rs, rs.GetOrdinal(name), session);
		}

		/// <summary></summary>
		public override System.Type ReturnedClass
		{
			get { return typeof(Guid); }
		}

		public override void Set(DbCommand cmd, object value, int index, ISessionImplementor session)
		{
			var dp = cmd.Parameters[index];
			//Modified by KIT
			var fullName = cmd.GetType().FullName;
			if (fullName != null && (fullName.Equals("Oracle.DataAccess.Client.OracleCommand") ||
			                         fullName.Equals("Oracle.ManagedDataAccess.Client.OracleCommand")))
			{
				// In case of Oracle data provider convert the Guid to a properly formatted string instead
				// of letting Oracle.DataAccess build an inappropriate format
				dp.Value = ((Guid)value).ToString("B"); // Enclose Guid in brackets like {34781E93-33B6-4b6b-8860-F52972DE77F0}
			}
			else
			{
				dp.Value = dp.DbType == DbType.Binary ? ((Guid)value).ToByteArray() : value;
			}
		}

		/// <summary></summary>
		public override string Name
		{
			get { return "Guid"; }
		}

		// Since 5.2
		[Obsolete("This method has no more usages and will be removed in a future version.")]
		public override object FromStringValue(string xml)
		{
			return new Guid(xml);
		}

		// 6.0 TODO: rename "xml" parameter as "value": it is not a xml string. The fact it generally comes from a xml
		// attribute value is irrelevant to the method behavior.
		/// <inheritdoc />
		public object StringToObject(string xml)
		{
			return string.IsNullOrEmpty(xml) ? null :
				// 6.0 TODO: inline the call.
#pragma warning disable 618
				FromStringValue(xml);
#pragma warning restore 618
		}

		public override System.Type PrimitiveClass
		{
			get { return typeof(Guid); }
		}

		public override object DefaultValue
		{
			get { return Guid.Empty; }
		}

		public override string ObjectToSQLString(object value, Dialect.Dialect dialect)
		{
			return "'" + value + "'";
		}
	}
}
