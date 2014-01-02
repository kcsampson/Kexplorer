using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CopyToClipboardFullNameScript.
	/// </summary>
	public class CopyToClipboardFullNameScript : BaseFileAndFolderScript
	{
		public CopyToClipboardFullNameScript()
		{
			this.LongName = "Name - Full Name to Clipboard";

			this.Description = "Copy the full name of the selected folder or file to the clipboard.";

			this.Active = true;


			this.ScriptShortCut = Shortcut.CtrlN;

		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			if ( files != null && files.Length > 0 )
			{
				Clipboard.SetDataObject( files[0].FullName );
			}
		}

		public override void Run(KExplorerNode folder)
		{
			Clipboard.SetDataObject( folder.DirInfo.FullName, true );
		}
	}
}
