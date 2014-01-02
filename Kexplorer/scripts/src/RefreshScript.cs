using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for RefreshScript.
	/// </summary>
	public class RefreshScript : BaseFileAndFolderScript
	{
		public RefreshScript()
		{
			this.LongName = "Refresh";

			this.Description = "Refresh the current folder";

			this.ScriptShortCut = Shortcut.F5;

			this.Active = true;
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			this.ScriptHelper.RefreshFolder( folder, false );
		}

		public override void Run(KExplorerNode folder)
		{
			this.ScriptHelper.RefreshFolder( folder, false);
		}
	}
}
