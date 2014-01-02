using System;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for DoShowFolderNode.
	/// </summary>
	public class MSExplorerHereFolderScript : BaseFolderScript
	{
        public MSExplorerHereFolderScript()
		{
			this.LongName = "MS Explorer Here";

			this.Description = "Launch MS Explorer at this folder.";

			this.Active = true;

			

		}

		public override void Run(KExplorerNode folder)
		{
			this.ScriptHelper.RunProgram("explorer.exe", " . ", folder,false );
		}
	}
}
