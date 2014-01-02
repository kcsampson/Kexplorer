using System;
using System.IO;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Run Beyond Compare
	/// </summary>
	public class BeyondCompareRunScript : BaseFileAndFolderScript
	{
		public BeyondCompareRunScript()
		{
			
			this.Description = "Run Beyond Compare to compare selected files/folders.";

			this.LongName = "Beyond Compare";

			this.Active = false;
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{

			ScriptRunParams runParams = (ScriptRunParams)this.ScriptHelper.VARS[
																	"BEYONDCOMPARELEFT"];


			if ( runParams != null )
			{
				if ( files != null && runParams.Files != null )
				{
					this.ScriptHelper.RunProgram(
						@"BC2.exe"
						, "\"" +   runParams.Files[0].FullName + "\"" +  
						" " + "\"" +   files[0].FullName + "\""
						, folder,false);
				} 
				else 
				{
					this.ScriptHelper.RunProgram(
						@"BC2.exe"
						, "\"" + runParams.FolderNode.DirInfo.FullName + "\"" 
						 + " " + "\"" + folder.DirInfo.FullName + "\""
						, folder,false);
				}
			}


		}

		public override void Run(KExplorerNode folder)
		{
			this.Run( folder, null );
		}
	}
}
