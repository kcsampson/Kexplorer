using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer
{
    public class KexplorerFtpNode : TreeNode
    {
        private FtpSite ftpSite = null;
        private String path = null;
        private bool loaded = false;
        private bool stale = false;
        
        public KexplorerFtpNode(FtpSite site, String path, String name)
        {

            this.Text = name;
            this.ftpSite = site;
            this.path = path;
                
        }


        public String Path
        {
            get { return this.path; }
        }

        public FtpSite Site
        {
            get { return this.ftpSite; }
        }

        public KexplorerFtpNode(FtpSite site)
        {

            this.Text = site.targetFolder + "@" + site.host;
            this.ftpSite = site;
            this.path = site.targetFolder;

        }

        /// <summary>
        /// Sets whether or not this object is stale and we should rebuild sub-nodes next time
        /// we process it.
        /// Sets all children stale.
        /// </summary>
        public bool Stale
        {
            get
            {
                return this.stale;
            }
            set
            {
                this.stale = value;
                if (this.stale)
                {
                    foreach (KexplorerFtpNode kNode in this.Nodes)
                    {
                        kNode.Stale = true;
                    }
                }
            }
        }


    }
}
