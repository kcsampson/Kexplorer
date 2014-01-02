using System;
using System.IO;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for BeyondCompareSetLeftSideScript.
	/// </summary>
	public class BeyondCompareSetLeftSideScript : BaseFileAndFolderScript
	{
		public BeyondCompareSetLeftSideScript()
		{
			this.Active = true;
			this.Description = "Set the selection file/folder as the left hand side of Beyond Compare";
			this.LongName = "Beyond Compare - Set Left Side";

			
		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			this.ScriptHelper.VARS["BEYONDCOMPARELEFT"] = new ScriptRunParams( folder, file );
			IScript beyondCompare = this.ScriptHelper.FindScript("Beyond Compare");
			if ( beyondCompare != null )
			{
				beyondCompare.Active = true;
			}
		}

		public override void Run(KExplorerNode folder)
		{
			this.Run( folder, null );
		}
	}
}
