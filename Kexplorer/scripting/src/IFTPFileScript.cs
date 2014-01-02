using System;
using System.Collections.Generic;
using System.Text;

namespace Kexplorer.scripting
{


    public interface IFTPFileScript : IScript
    {
        void Run( KexplorerFtpNode folder, string[] files );



    }
}




