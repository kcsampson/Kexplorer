using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CommandPromptScript.
	/// </summary>
	public class CommandPromptScript : BaseFileAndFolderScript
	{
		public CommandPromptScript()
		{
			
			this.LongName =  "DOS Window";

			this.Description ="Open a DOS windows at the current folder.";

			this.Active = true;

			this.ScriptShortCut = Shortcut.F2;
		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			this.Run( folder );
		}

		public override void Run(KExplorerNode folder)
		{
			this.ScriptHelper.RunProgram( "cmd","", folder ,false);
		}
	}
}
