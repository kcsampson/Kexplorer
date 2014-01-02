using System;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for DoShowFolderNode.
	/// </summary>
	public class DoShowFolderNode : BaseFolderScript
	{
		public DoShowFolderNode()
		{
			this.LongName = "Remove from view";

			this.Description = "Remove the selected folder from view. " +
				"If it's a drive, it won't show until restarting the application.";

			this.Active = true;

            this.ScriptShortCut = System.Windows.Forms.Shortcut.F8;

			

		}

		public override void Run(KExplorerNode folder)
		{
			folder.Remove();
		}
	}
}
