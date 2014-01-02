using System;
using System.Windows.Forms;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for MakeDirectoryScript.
	/// </summary>
	public class MakeDirectoryScript : BaseFolderScript
	{
		public MakeDirectoryScript()
		{
			this.LongName = "Make Directory";

			this.Active = true;

			this.Description = "Make a sub-directory under the selected folder.";

			
		}

		public override void Run(KExplorerNode folder)
		{
			string newDir = QuickDialog.DoQuickDialog( "Make Directory", "Sub-folder of: " + folder.DirInfo.Name
															,"");

			if ( newDir != null )
			{
				if ( newDir.Length == 0 )
				{
					newDir = QuickDialog.DoQuickDialog( "Make Directory"
											,"Folder can't be empty. Use Cancel to abort"
											,"");
				} 
				if ( newDir != null && newDir.Length > 0 )
				{
					// Go ahead and make the directory.

					try 
					{ 

						folder.DirInfo.CreateSubdirectory( newDir );
					} 
					catch (Exception e )
					{
						System.Windows.Forms.MessageBox.Show( "Exception: " + e.Message
														, "Make Direory error"
														, MessageBoxButtons.OK);
					}

					this.ScriptHelper.RefreshFolder( folder, true );
				}

			}
		}
	}
}
