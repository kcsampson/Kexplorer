using System;
using System.Collections.Generic;
using System.Text;

namespace Kexplorer
{
    public class FtpSite
    {
        private static FtpSite lockInstance = new FtpSite();
        public static FtpSite LockInstance(){
            return lockInstance;
        }

        public String host = null;
        public String username = null;
        public String pwd = null;
        public String targetFolder = null;
        public String type = null;

        public FtpSite(String host, String username, String pwd, String targetFolder, String type )
        {
            this.host = host;
            this.username = username;
            this.pwd = pwd;
            this.targetFolder = targetFolder;
            this.type = type;
        }
        public FtpSite() {
           
        }
    }
}
