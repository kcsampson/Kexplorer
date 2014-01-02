using System;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Use specifies, hey, next time I unzip a file, it should unzip to here.
	/// </summary>
	public class ZipNextUnZipLocFolderScript : BaseFolderScript
	{
        static public string ZIP_NEXTUNZIP_LONG_NAME = "Zip - Next Unzip Location";
		public ZipNextUnZipLocFolderScript()
		{
			
			this.LongName = ZIP_NEXTUNZIP_LONG_NAME;

			this.Description = "Specifies the selected folder and the next unzip target location";

			this.Active = true;


		}

		public override void Run(KExplorerNode folder)
		{
			this.ScriptHelper.VARS["NEXTUNZIPLOCATION"] = folder;


            IScript unzipTo = this.ScriptHelper.FindScript(ZipUnzipToFileScript.ZIP_UNZIP_TO_LONG_NAME);
			if ( unzipTo != null )
			{
				unzipTo.Active = true;
			}
		}
	}
}
