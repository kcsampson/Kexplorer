using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using Kexplorer.scripts;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for MakeWritableScript.
	/// </summary>
	public class MakeWritableScript : BaseFileScript
	{
		public MakeWritableScript()
		{
			
			this.LongName = "Toggle Readonly";

			this.Active = true;

			this.ScriptShortCut = Shortcut.CtrlR;

			this.Validator = new FileValidator( this.CheckReadOnly );
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			foreach ( FileInfo file in files )
			{
				file.Attributes = FileAttributes.Normal;
			}

			this.ScriptHelper.RefreshFolder( folder, false );
		}


		private bool CheckReadOnly( FileInfo file )
		{
			return ( ( file.Attributes & FileAttributes.ReadOnly ) ==
					FileAttributes.ReadOnly );
			
		}




	}
}
