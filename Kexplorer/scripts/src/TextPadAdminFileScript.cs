using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kexplorer.scripts
{
    class TextPadAdminFileScript : BaseFileScript
	{
        public TextPadAdminFileScript()
		{
			this.LongName = "Notepad++ as Admin";

			this.Active = true;

			this.Description = "Run Notepad++ on selected file as Admin";
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			this.ScriptHelper.RunProgram("TextPad.exe"," " + files[0].FullName, folder, true );
		}
    }
}
