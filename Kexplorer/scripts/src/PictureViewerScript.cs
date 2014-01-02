using System;
using System.IO;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for PictureViewerScript.
	/// </summary>
	public class PictureViewerScript : BaseFileScript
	{
		public PictureViewerScript()
		{

			this.LongName = "Image Viewer";

			this.Active = true;

			this.Description = "Run the Image Viewer for the selected file.";


			this.ValidExtensions = new string[] 
				{".jpg",".jpeg",".gif" };
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			//<Launcher ext="jpg" command="rundll32.exe" preOption= C:\WINDOWS\System32\shimgvw.dll,ImageView_Fullscreen" />
            
			this.ScriptHelper.RunProgram( "rundll32.exe"
				//,"c:\\WINDOWS\\System32\\shimgvw.dll,ImageView_Fullscreen " + files[0].FullName
                , "shimgvw.dll,ImageView_Fullscreen " + files[0].FullName
					, folder, false );
		}
	}
}
