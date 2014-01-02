using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for TextpadNewFileScript.
	/// </summary>
	public class TextpadNewFileScript : BaseFolderScript
	{
		public TextpadNewFileScript()
		{
			this.LongName = "Notepad++ newfile.txt here";

			this.ScriptShortCut = Shortcut.CtrlT;

			this.Description = "Runs Notepad on a new file created in this folder.";
		}

		public override void Run(KExplorerNode folder)
		{
			StreamWriter combine;
			FileInfo fInfo = new FileInfo( folder.DirInfo.FullName + "\\newfile.txt" );
			if (!fInfo.Exists )
			{
				combine=File.CreateText(folder.DirInfo.FullName + "\\newfile.txt" );
				combine.Close();
			}

			this.ScriptHelper.RunProgram("TextPad.exe","-q" + fInfo.FullName, folder,false );

		}
	}
}
