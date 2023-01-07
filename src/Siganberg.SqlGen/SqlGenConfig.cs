using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Siganberg.SqlGen;

public class SqlGenConfig
{
    public string Server { get; set; }
    public string TargetPath { get; set; }
    public List<Database> Databases { get; set; }

    public SqlGenConfig()
    {
        Databases = new List<Database>();
    }
    
    public class Database
    {
        public string Name { get; set; }
        public string FolderName { get; set; }
        [JsonIgnore]
        public string GeneratedName => string.IsNullOrEmpty(FolderName) ? Name : FolderName;
        public HashSet<string> Tables { get; set; }
        public HashSet<string> StoredProcedures { get; set; }
        public HashSet<string> Views { get; set; }
        public Database()
        {
            StoredProcedures = new HashSet<string>();
            Tables = new HashSet<string>();
            Views = new HashSet<string>();
        }
    }
}