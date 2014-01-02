using System;
using System.Collections.Generic;
using System.Text;

namespace Kexplorer.scripting
{
    public interface IFtpFolderScript : IScript
    {
        void Run( KexplorerFtpNode folder);
    }
}
