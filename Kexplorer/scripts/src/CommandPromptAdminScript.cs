using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kexplorer.scripts
{
    class CommandPromptAdminScript: BaseFileAndFolderScript
	{
        public CommandPromptAdminScript()
		{
			
			this.LongName =  "DOS Window Admin";

			this.Description ="Open a DOS windows at the current folder in Admin mode";

			this.Active = true;

		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			this.Run( folder );
		}

		public override void Run(KExplorerNode folder)
		{
			this.ScriptHelper.RunProgram( "cmd","", folder ,true);
		}
    }
}
