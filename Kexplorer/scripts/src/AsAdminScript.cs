using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;

namespace Kexplorer.scripts
{
    class AsAdminScript : BaseFileScript
	{
        public AsAdminScript()
		{
			this.LongName = "As Admin Run";

			this.Description = "Run Program as Admin.";

			this.Active = true;

            this.ValidExtensions = new string[] { ".cmd", ".bat", ".exe" };

		}

        public override void Run(KExplorerNode folder, FileInfo[] xfiles)
        {


            foreach (var xfile in xfiles)
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = folder.DirInfo.FullName,
                    FileName =xfile.FullName
                };

                if (!IsAdministrator())
                {
                    info.Verb = "runas";
                }


                var p = Process.Start(info);
               


            }
        }

            public static bool IsAdministrator()
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();

                if (null != identity)
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }

                return false;
            }

        
    
    }
}
