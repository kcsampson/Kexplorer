using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CopyFileScript.
	/// </summary>
	public class CutScript : BaseFileAndFolderScript
	{
		public CutScript()
		{
			this.LongName = "Edit - Cut";

			this.Description = "Cut selected files or folders";

			this.ScriptShortCut = Shortcut.CtrlX;


			this.Active = true;

		}


		/// <summary>
		/// Set the copied file(s).  If there were CUT files, reset them.
		/// </summary>
		/// <param name="folder"></param>
		/// <param name="files"></param>
		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			this.ScriptHelper.VARS["CUTFILES"] = new ScriptRunParams( folder, files );
			this.ScriptHelper.VARS["COPYFILES"] = null;
			IScript paste = this.ScriptHelper.FindScript(PasteScript.LONG_NAME);
			if ( paste != null )
			{
				paste.Active = true;
			}
		}

		public override void Run(KExplorerNode folder)
		{
			this.Run( folder, null );
		}

	}
}
