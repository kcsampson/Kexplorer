using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CopyToNameToCipboardForTSClientScript.
	/// </summary>
	public class CopyToNameToCipboardForTSClientScript : BaseFileAndFolderScript
	{
		public CopyToNameToCipboardForTSClientScript()
		{
            this.LongName = "Name - TSS-Name to Clipboard"; ;

			this.Description = "Copy the full name of the selected folder or file to the clipboard for pasting transfers in Terminal Server Client.";

			this.Active = true;


			this.ScriptShortCut = Shortcut.CtrlM;
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			string temp = files[0].FullName;

			string tssName = "\""
				              + @"\\tsclient\\"
							+ temp.Replace(":","") + "\"";


			if ( files != null && files.Length > 0 )
			{
				Clipboard.SetDataObject(tssName );
			}
		}

		public override void Run(KExplorerNode folder)
		{

			string temp = folder.DirInfo.FullName;

			string tssName = "\"" + @"\\tsclient\\" + temp.Replace(":","") + "\"";

			Clipboard.SetDataObject( tssName, true );
		}
	}
}
