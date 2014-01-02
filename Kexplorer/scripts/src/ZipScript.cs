using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for ZipScript.
	/// </summary>
	public class ZipScript : BaseFileAndFolderScript
	{
		public ZipScript()
		{
			this.LongName = "Zip with 7z";

			this.Description = "Zip the selected folder or files..";

			
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			string newZipName = QuickDialog.DoQuickDialog( "Create Zip"
				, "Zip Name"
				, "..\\"+folder.Text );


			if ( newZipName != null && newZipName.Length > 0 )
			{		
				if ( !newZipName.ToLower().EndsWith(".zip"))
				{
					newZipName = newZipName + ".zip";

				}

				string listfile = Application.StartupPath + "\\zipfilelist.lst";

				this.BuildListFile( listfile , files );
						
				
				this.ScriptHelper.RunProgram( @"7z.exe"
					, "a -tzip " + "\"" +newZipName + "\" @\"" + listfile + "\""
					, folder );		
			}
		}

		public override void Run(KExplorerNode folder)
		{
			string newZipName = QuickDialog.DoQuickDialog( "Create Zip"
				, "Zip Name"
				, "..\\"+folder.Text );


			if ( newZipName != null && newZipName.Length > 0 )
			{		
				if ( !newZipName.ToLower().EndsWith(".zip"))
				{
					newZipName = newZipName + ".zip";

				}


				this.ScriptHelper.RunProgram( @"7z.exe"
					, "a -tzip " + "\"" +newZipName + "\" -r \"" + folder.DirInfo.FullName + "\"" 
					, folder );		
			}
		}



		private void BuildListFile( string fileName, FileInfo[] files )
		{

			StreamWriter writer = File.CreateText( fileName );

			foreach ( FileInfo file in files )
			{
				writer.WriteLine( file.FullName );
			}

			writer.Flush();

			writer.Close();
		}
	}
}
