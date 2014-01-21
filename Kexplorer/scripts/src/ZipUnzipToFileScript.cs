using System;
using System.IO;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for UnzipToFileScript.
	/// </summary>
	public class ZipUnzipToFileScript : BaseFileScript
	{
        public static string ZIP_UNZIP_TO_LONG_NAME = "Zip - Unzip To";


		public ZipUnzipToFileScript()
		{
            this.LongName = ZIP_UNZIP_TO_LONG_NAME;

			this.Description = "Unzip to the previously selected folder";

			this.Active = false;

            this.ValidExtensions = new string[] { ".zip", ".7z", ".jar", ".docx", ".xlsx", "pptx", "tar","gz","gzip" };
		}


		// Unzip to the specified folder....
		//

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{

			KExplorerNode destNode = (KExplorerNode)this.ScriptHelper.VARS["NEXTUNZIPLOCATION"];

			if ( destNode != null )
			{

				//  C:\Program Files\7-Zip\7z.exe" preOption = "x"
				this.ScriptHelper.RunProgram( @"C:\Program Files\7-Zip\7z.exe"
					, "x " + "\"" +file[0].FullName + "\" -o\"" + destNode.FullPath + "\"" 
					, destNode,false );

				this.ScriptHelper.RefreshFolder( folder, false );
			}
		}
	}
}
