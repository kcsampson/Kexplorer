using System;
using System.IO;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for UnzipHereFileScript.
	/// </summary>
	public class UnzipHereFileScript : BaseFileScript
	{
		public UnzipHereFileScript()
		{
			this.LongName = "Zip - Unzip Here";

			this.Description = "Unzip the selected zip file here.";

			this.Active = true;


			this.ValidExtensions = new string[]{ ".zip", ".7z",".docx",".xlsx","pptx",". gzip",".gz",".tar" };
		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{

			//  C:\Program Files\7-Zip\7z.exe" preOption = "x"
			this.ScriptHelper.RunProgram( @"7z.exe"
				, "x " + "\"" +file[0].FullName + "\" -o\"" + folder.DirInfo.FullName + "\"" 
				, folder,false );

			this.ScriptHelper.RefreshFolder( folder, false );
			
		}
	}
}
