using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for RenameScript.
	/// </summary>
	public class RenameScript : BaseFileAndFolderScript
	{
		public RenameScript()
		{
			this.LongName = "Rename";

			this.Description = "Rename the selected folder or file.";

			this.ScriptShortCut = Shortcut.CtrlR;
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			if ( files == null || files.Length > 1 )
			{
				return;
			}
			string newName = QuickDialog.DoQuickDialog( "Rename Folder", "New Name", files[0].Name );

			if ( newName != null && newName.Length > 0 )
			{
				if (!newName.Equals(files[0].Name ))
				{

					string x = files[0].DirectoryName + "\\" + newName;
					files[0].MoveTo(x);


					this.ScriptHelper.RefreshFolder( folder, false );
					
				}
			}
		}

		public override void Run(KExplorerNode folder)
		{
			string newName = QuickDialog.DoQuickDialog( "Rename Folder", "New Name", folder.DirInfo.Name );

			if ( newName != null && newName.Length > 0 )
			{
				if (!newName.Equals(folder.DirInfo.Name ))
				{

					string x = folder.DirInfo.Parent.FullName + "\\" + newName;
					folder.DirInfo.MoveTo(x);


					this.ScriptHelper.RefreshFolder( folder, true );


				}
			}
		}

	}
}
