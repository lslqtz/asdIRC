using System.Data;
using MySql.Data.MySqlClient;

namespace asdIRC.Helpers
{
    internal static class Database
    {
        private static string _connectionString;
        public static string ConnectionString
        {
            get { return _connectionString; }
            set
            {
                _connectionString = value;
                HasDatabase = !string.IsNullOrEmpty(value);
                MySqlConnection.ClearAllPools();
            }
        }

        private static string _connectionStringSlave;
        public static string ConnectionStringSlave
        {
            get { return _connectionStringSlave; }
            set
            {
                _connectionStringSlave = value;
                MySqlConnection.ClearAllPools();
            }
        }

        public static bool HasDatabase = false;

        internal static MySqlConnection GetConnection(bool useSlave = false)
        {
            return new MySqlConnection(useSlave ? ConnectionStringSlave : ConnectionString);
        }

        internal static MySqlDataReader runQuery(MySqlConnection m, string sqlString, params MySqlParameter[] parameters)
        {
            m.Open();
            MySqlCommand c = m.CreateCommand();
            if (parameters != null)
                c.Parameters.AddRange(parameters);
            c.CommandText = sqlString;
            c.CommandTimeout = 5;
            return c.ExecuteReader(CommandBehavior.CloseConnection);
        }

        internal static MySqlDataReader RunQuery(string sqlString, params MySqlParameter[] parameters)
        {
            if (!HasDatabase) return null;
            return runQuery(GetConnection(), sqlString, parameters);
        }

        internal static MySqlDataReader RunQuerySlave(string sqlString, params MySqlParameter[] parameters)
        {
            if (!HasDatabase) return null;
            return runQuery(GetConnection(true), sqlString, parameters);
        }

        internal static object RunQueryOne(string sqlString, params MySqlParameter[] parameters)
        {
            if (!HasDatabase) return 0;
            using (MySqlConnection m = GetConnection())
            {
                m.Open();
                using (MySqlCommand c = m.CreateCommand())
                {
                    c.Parameters.AddRange(parameters);
                    c.CommandText = sqlString;
                    c.CommandTimeout = 5;
                    return c.ExecuteScalar();
                }
            }
        }

        internal static int RunNonQuery(string sqlString, params MySqlParameter[] parameters)
        {
            if (!HasDatabase) return 0;

            using (MySqlConnection m = GetConnection())
            {
                m.Open();
                using (MySqlCommand c = m.CreateCommand())
                {
                    c.Parameters.AddRange(parameters);
                    c.CommandText = sqlString;
                    c.CommandTimeout = 5;
                    return c.ExecuteNonQuery();
                }
            }
        }

        internal static DataSet RunDataset(string sqlString, params MySqlParameter[] parameters)
        {
            if (!HasDatabase) return null;

            using (MySqlConnection m = GetConnection())
            {
                m.Open();
                return MySqlHelper.ExecuteDataset(m, sqlString, parameters);
            }
        }
    }
}