using System;
using System.IO;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for UnzipHereFileScript.
	/// </summary>
	public class ZipOpenFileScript : BaseFileScript
	{
        public ZipOpenFileScript()
		{
			this.LongName = "Zip - 7z View";

			this.Description = "View Contents of archive with 7zip";

			this.Active = true;


            this.ValidExtensions = new string[] { ".zip", ".7z", ".jar", ".docx", ".xlsx", "pptx" };
		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{

			//  C:\Program Files\7-Zip\7z.exe" preOption = "x"
			this.ScriptHelper.RunProgram( @"7zFM.exe"
				, " " + "\"" +file[0].FullName + "\" -o\"" + folder.DirInfo.FullName + "\"" 
				, folder,false );

			this.ScriptHelper.RefreshFolder( folder, false );
			
		}
	}
}
