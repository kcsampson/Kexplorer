using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CopyFileScript.
	/// </summary>
	public class ResetConfigScript : BaseFileAndFolderScript
	{
        public ResetConfigScript()
		{
			this.LongName = "Reset Configuration of KExplorer";

			this.Description = "Reloads ScriptHelper.xml";

			this.ScriptShortCut = Shortcut.CtrlF5;


			this.Active = true;

		}


		/// <summary>
		/// Set the copied file(s).  If there were CUT files, reset them.
		/// </summary>
		/// <param name="folder"></param>
		/// <param name="files"></param>
		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
            this.ScriptHelper.ScriptHelperDoc = null;
		}

		public override void Run(KExplorerNode folder)
		{
			this.Run( folder, null );
		}

	}
}
