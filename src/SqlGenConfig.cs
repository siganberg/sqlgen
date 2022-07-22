using System.Collections.Generic;
using System.Linq;

namespace Siganberg.SqlGen;

public class SqlGenConfig
{
    public string Server { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    
    public string TargetPath { get; set; }
    public IEnumerable<Database> Databases { get; set; }

    public SqlGenConfig()
    {
        Databases = Enumerable.Empty<Database>();
    }
    
    public class Database
    {
        public string Name { get; set; }
        public IEnumerable<string> Tables { get; set; }
        public IEnumerable<string> StoredProcedures { get; set; }
        public string FolderName { get; set; }

        public string GeneratedName => string.IsNullOrEmpty(FolderName) ? Name : FolderName;

        public Database()
        {
            StoredProcedures = Enumerable.Empty<string>();
            Tables = Enumerable.Empty<string>();
        }
    }
}