using System.Text;

namespace Greatbone.Core
{
    /// <summary>
    /// The configuration for a service instance which is constructed programmatically or loaded from file.
    /// </summary>
    public class ServiceConfig : WorkConfig, IData
    {
        // the shard id of the service instance, can be null
        public string shard;

        // the bound addresses 
        public string[] addrs;

        // two ints for enc/dec authentication token
        public long cipher;

        // db configuration
        public Db db;

        // cluster members in the form of peerid-address pairs
        public Map<string, string> cluster;

        // logging level
        public int logging = 3;

        // shared cache or not
        public bool cache;

        public ServiceConfig(string name) : base(name)
        {
        }

        volatile string connstr;

        public string ConnectionString
        {
            get
            {
                if (connstr == null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Host=").Append(db.host);
                    sb.Append(";Port=").Append(db.port);
                    sb.Append(";Database=").Append(db.database ?? Name);
                    sb.Append(";Username=").Append(db.username);
                    sb.Append(";Password=").Append(db.password);
                    sb.Append(";Read Buffer Size=").Append(1024 * 32);
                    sb.Append(";Write Buffer Size=").Append(1024 * 32);
                    sb.Append(";No Reset On Close=").Append(true);

                    connstr = sb.ToString();
                }
                return connstr;
            }
        }

        public void Read(ISource s, byte proj = 0x0f)
        {
            s.Get(nameof(shard), ref shard);
            s.Get(nameof(addrs), ref addrs);
            s.Get(nameof(db), ref db);
            s.Get(nameof(cluster), ref cluster);
            s.Get(nameof(logging), ref logging);
            s.Get(nameof(cache), ref cache);
        }

        public void Write<R>(ISink<R> s, byte proj = 0x0f) where R : ISink<R>
        {
            s.Put(nameof(shard), shard);
            s.Put(nameof(addrs), addrs);
            s.Put(nameof(db), db);
            s.Put(nameof(cluster), cluster);
            s.Put(nameof(logging), logging);
            s.Put(nameof(cache), cache);
        }
    }

    ///
    /// The DB configuration embedded in a service context.
    ///
    public class Db : IData
    {
        // IP host or unix domain socket
        public string host;

        // IP port
        public int port;

        // default database name
        public string database;

        public string username;

        public string password;

        public void Read(ISource s, byte proj = 0x0f)
        {
            s.Get(nameof(host), ref host);
            s.Get(nameof(port), ref port);
            s.Get(nameof(database), ref database);
            s.Get(nameof(username), ref username);
            s.Get(nameof(password), ref password);
        }

        public void Write<R>(ISink<R> s, byte proj = 0x0f) where R : ISink<R>
        {
            s.Put(nameof(host), host);
            s.Put(nameof(port), port);
            s.Put(nameof(database), database);
            s.Put(nameof(username), username);
            s.Put(nameof(password), password);
        }
    }
}