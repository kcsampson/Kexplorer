using System;
using System.IO;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for BeyondCompareSetLeftSideScript.
	/// </summary>
	public class SetAsXmlInputToXsltScript : BaseFileScript
	{
		public SetAsXmlInputToXsltScript()
		{
			this.Active = true;
			this.Description = "Set the selected Xml file as XML input to XSLT";
			this.LongName = "XSLT Set as XML Source";

			this.ValidExtensions = new string[] {".xml" };

			
		}

		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			this.ScriptHelper.VARS["SetAsXmlInputToXsltScript"] = new ScriptRunParams( folder, file );
			IScript XSLTRun = this.ScriptHelper.FindScript(XSLTRunScript.XSLT_RUN_SCRIPT_NAME);
			if ( XSLTRun != null )
			{
				XSLTRun.Active = true;
			}
		}


	}
}
