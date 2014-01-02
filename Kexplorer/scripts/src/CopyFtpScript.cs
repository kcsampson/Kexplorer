using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts.src
{
    public class CopyFtpScript : BaseFtpFileScript
    {

        public CopyFtpScript()
		{
			this.LongName = "Edit - Copy";

			this.Description = "Copy selected files or folders";


			this.Active = true;

			this.ScriptShortCut = Shortcut.CtrlC;

		}


        public override void Run(KexplorerFtpNode folder, string[] files)
        {
            
            this.ScriptHelper.VARS["COPYFILES"] = new ScriptRunParams(folder, files);
            this.ScriptHelper.VARS["CUTFILES"] = null;
            IScript paste = this.ScriptHelper.FindScript(PasteScript.LONG_NAME);
            if (paste != null)
            {
                paste.Active = true;
            }
        }
    }
}
