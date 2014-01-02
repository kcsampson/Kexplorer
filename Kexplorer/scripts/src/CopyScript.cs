using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CopyFileScript.
	/// </summary>
	public class CopyScript : BaseFileAndFolderScript
	{
		public CopyScript()
		{
			this.LongName = "Edit - Copy";

			this.Description = "Copy selected files or folders";


			this.Active = true;

			this.ScriptShortCut = Shortcut.CtrlC;

		}


		/// <summary>
		/// Set the copied file(s).  If there were CUT files, reset them.
		/// </summary>
		/// <param name="folder"></param>
		/// <param name="files"></param>
		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			this.ScriptHelper.VARS["COPYFILES"] = new ScriptRunParams( folder, files );
			this.ScriptHelper.VARS["CUTFILES"] = null;
			IScript paste = this.ScriptHelper.FindScript(PasteScript.LONG_NAME);
			if ( paste != null )
			{
				paste.Active = true;
			}

		
		}

		public override void Run(KExplorerNode folder)
		{
			if ( folder.Parent == null )
			{
				MessageBox.Show("Unable to copy drives.", "Copy Error"
								, MessageBoxButtons.OK );
				return;
			}
			this.Run( folder, null );
		}

	}
}
