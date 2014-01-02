using System;
using System.IO;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Used to be Textpad.exe, now Notepad. (Handled in ScriptHelper.xml where TextPad.exe is mapped to Notepad++.exe.
	/// </summary>
	public class TextPadFileScript : BaseFileScript
	{
		public TextPadFileScript()
		{
			this.LongName = "Notepad++";

			this.Active = true;

			this.Description = "Run Notepad++ on selected file.";
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			this.ScriptHelper.RunProgram("TextPad.exe"," " + files[0].FullName, folder, false );
		}
	}
}
