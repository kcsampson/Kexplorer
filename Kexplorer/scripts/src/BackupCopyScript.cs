using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for BackupCopyScript.
	/// </summary>
	public class BackupCopyScript : BaseFileAndFolderScript
	{
		public BackupCopyScript()
		{
		
			this.LongName = "Backup Copy Here";

			this.Description = "Make a backup copy of the selected files or folders.  Use is prompted for a prefix name.";

			this.Active = true;
		}

		#region Public Methods
		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			string targetFile = folder.DirInfo.FullName
				+ "\\"
				+ file[0].Name + ".bak";

			file[0].CopyTo( targetFile, true  );;
		}

		public override void Run(KExplorerNode folder)
		{
			KExplorerNode parentNode = (KExplorerNode)folder.Parent;
			if ( parentNode == null )
			{
				MessageBox.Show( "Can't backup drives", "Error: Backup Folder", MessageBoxButtons.OK );
				return;
			}

			string backupFolderName = QuickDialog.DoQuickDialog( "Backup Copy of Folder"
															, "Enter name of new backup folder"
														, folder.DirInfo.Name + ".bak");

			if ( backupFolderName !=  null )
			{
				if ( backupFolderName.Length == 0 )
				{
					MessageBox.Show( "Name can't be empty.", "Error: Backup Folder", MessageBoxButtons.OK );

				} 
				else 
				{


					DirectoryInfo backupFolder = parentNode.DirInfo.CreateSubdirectory( backupFolderName );


					this.TransferFilesAndFolders( folder.DirInfo, backupFolder );


					this.ScriptHelper.RefreshFolder( parentNode, true );

				}
			}
		}


		#endregion



		#region Private Methods


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Transfer everything inside the source folder to the target folder.  
		/// Names may stay the same
		/// </summary>
		/// <param name="sourceDir"></param>
		/// <param name="targetDir"></param>
		private void TransferFilesAndFolders( DirectoryInfo sourceDir
											, DirectoryInfo targetDir )
		{

			foreach ( FileInfo file in sourceDir.GetFiles() )
			{
				string targetFile = targetDir.FullName 
					+ "\\"
					+ file.Name;

				file.CopyTo( targetFile, true  );

			}

			foreach ( DirectoryInfo dir in sourceDir.GetDirectories())
			{

				DirectoryInfo newFolder = targetDir.CreateSubdirectory( dir.Name );

				this.TransferFilesAndFolders( dir, newFolder );
				
			}
		}


		#endregion
	}
}
