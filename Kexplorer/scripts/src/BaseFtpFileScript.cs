using System;
using System.Collections.Generic;
using System.Text;
using Kexplorer.scripting;

namespace Kexplorer.scripts 
{
    public abstract class BaseFtpFileScript : BaseScript, IFTPFileScript
    {

        public BaseFtpFileScript()
        {
        }

        public abstract void Run(KexplorerFtpNode folder, string[] files);
    }
}
