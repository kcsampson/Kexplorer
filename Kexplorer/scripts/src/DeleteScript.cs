using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using System.Linq;
using System.Net;
using System.Threading;
using Kexplorer.res;


namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for DeleteScript.
	/// </summary>
    public class DeleteScript : BaseFileAndFolderScript, IFTPFileScript
	{
		private int totalFileCount = 0;
		public DeleteScript()
		{
			this.LongName = "Edit- Delete";

			this.Description = "Delete Selected File or Folder";

			this.Active = true;


			this.ScriptShortCut = Shortcut.Del;
		
		}

        private Thread workerThread = null;

        private KexplorerFtpNode folder;
        private string[] files;
        private bool cancelled = false;


        private void FtpCancelled()
        {
            this.cancelled = true;
        }

        private SimpleProgress progress = null;
        public void Run(KexplorerFtpNode xfolder, string[] xfiles)
        {
            if (xfiles == null)
            {
                MessageBox.Show("FTP Delete only supported for individual files", "Kexplorer");
                return;

            }

            folder = xfolder;
            files = xfiles;

            this.workerThread = new Thread(new ThreadStart(this.StartFtpDeleteInThread));
            this.workerThread.Start();
            this.cancelled = false;


            progress = SimpleProgress.StartProgress(this.FtpCancelled
                                            , "Kexplorer FTP Delete"
                                            , "Starting...");



            while (!this.cancelled && this.workerThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(250);
            }

            this.ScriptHelper.RefreshFolder( folder, false );

        }

		public override void Run(KExplorerNode folder, FileInfo[] files )
		{
			KExplorerNode parentNode = (KExplorerNode)folder.Parent;
			if ( parentNode == null )
			{
				MessageBox.Show( "Kexplorer won't delete at root, or root."
					,"Kexplorer Caution"
					,MessageBoxButtons.OK );
				return;
			}
			if ( files == null )
			{
				this.DeleteFolder( folder );

				this.ScriptHelper.RefreshFolder( parentNode, false );

			} 
			else 
			{
				// The user consciously selected files in the righthand side, and 
				// selected delete.. Should we Confirm?????
				// Probably.
				
				this.DeleteFiles( files );
				this.ScriptHelper.RefreshFolder( folder, false );
			}
			
		}

		public override void Run(KExplorerNode folder)
		{
			this.Run( folder, null );
		}


		private void DeleteFiles( FileInfo[] files )
		{

			if ( files.Length > 1 )
			{
				DialogResult result = MessageBox.Show( "Delete multiple files?"
											,"Delete Multiple files"
											, MessageBoxButtons.YesNoCancel );

				if ( result != DialogResult.Yes )
				{
					return;
				}
			}
			
			foreach (FileInfo file in files )
			{
				
				if ((file.Attributes & FileAttributes.ReadOnly) ==
					FileAttributes.ReadOnly)
				{
					DialogResult result = MessageBox.Show("File: " + file.Name
					   + " is readonly.  Delete?"
				, "Deletion Readonly Files?"
						,System.Windows.Forms.MessageBoxButtons.YesNo
						,System.Windows.Forms.MessageBoxIcon.Exclamation
						, System.Windows.Forms.MessageBoxDefaultButton.Button2 );

					if ( result != DialogResult.Yes )
					{
						continue;
					} 
					else
					{
						// Have to change all the readonly files.
						file.Attributes = FileAttributes.Normal;
					
					}

				}
				file.Delete();
			}
		}


		/// <summary>
		/// Delete Folder Control.  Check readonly ahead of time.
		/// </summary>
		/// <param name="folder"></param>
		private void DeleteFolder( KExplorerNode folder )
		{
			
			this.totalFileCount = 0;
			ArrayList readOnlyFiles = this.CheckForReadOnly( folder.DirInfo );

			

			if ( readOnlyFiles.Count > 0 )
			{

				
				DialogResult result = MessageBox.Show("There were: " + readOnlyFiles.Count.ToString()
					+ " Readonly files to be deleted.  Continue?"
					, "Deletion Readonly Files?"
					,System.Windows.Forms.MessageBoxButtons.YesNo
					,System.Windows.Forms.MessageBoxIcon.Exclamation
					, System.Windows.Forms.MessageBoxDefaultButton.Button2 );

				if ( result != DialogResult.Yes )
				{
					return;
				} 
				else
				{
					// Have to change all the readonly files.
					this.ChangeReadonlyFiles( readOnlyFiles );
					
				}

			} 
			else if ( this.totalFileCount > 0 )
			{
				DialogResult result = MessageBox.Show( "Delete folder and all " + this.totalFileCount.ToString()
									+ " file contained herein?"
									, "Delete Files/Folders"
									, MessageBoxButtons.YesNo );

				if ( result != DialogResult.Yes )
				{
					return;
				}
			}

			KExplorerNode parentNode = (KExplorerNode)folder.Parent;

			if ( parentNode != null )
			{
				this.DeleteAllFilesInFolder( folder.DirInfo );

				try 
				{
					Directory.Delete( folder.FullPath, true );
				} 
				catch (Exception e ){
					MessageBox.Show("Delete Error", "Exception: " + e.Message
									, MessageBoxButtons.OK );
				}

			
				//this.ScriptHelper.RefreshFolder( parentNode, true );
			} 
			else 
			{
				MessageBox.Show( "Unable to delete drives."
								,"Delete Error"
								, MessageBoxButtons.OK );
			}

			

		}

		private void ChangeReadonlyFiles( ArrayList readonlyFiles )
		{
			foreach ( FileInfo file in readonlyFiles )
			{

				file.Attributes = FileAttributes.Normal;
				
			}
		}

		/// <summary>
		/// Return all file infos of read only files.
		/// </summary>
		/// <param name="folder"></param>
		/// <returns></returns>
		private ArrayList CheckForReadOnly( DirectoryInfo folder )
		{
			
			ArrayList readonlyFiles = new ArrayList();

			foreach ( FileInfo file in folder.GetFiles() )
			{
				++this.totalFileCount;

				if ((file.Attributes & FileAttributes.ReadOnly) ==
					FileAttributes.ReadOnly)
				{
					readonlyFiles.Add( file );
				}
			}
			foreach ( DirectoryInfo dir in folder.GetDirectories())
			{
				readonlyFiles.AddRange( this.CheckForReadOnly( dir ));


			}

			return readonlyFiles;

		}

		private void DeleteAllFilesInFolder( DirectoryInfo folder )
		{
			try 
			{

				foreach ( FileInfo file in folder.GetFiles() )
				{
				
					file.Delete();
				}
				foreach ( DirectoryInfo dir in folder.GetDirectories())
				{
					this.DeleteAllFilesInFolder( dir );

				}
			} 
			catch ( Exception e )
			{
				Console.WriteLine("Exception deleting..." + e.Message );
			}
		}




        private void StartFtpDeleteInThread()
        {
            try
            {
                Thread.Sleep(50);

                String host = folder.Site.host;
                String username = folder.Site.username;
                String pwd = folder.Site.pwd;

                

                String targetfolder = folder.Path;

              if ( files != null ){
                int count = files.Count();
                int iFileCount = 0;


                foreach (var file in files)
                {
                    string basicProgress = (++iFileCount).ToString() + " of " + count.ToString() + " : " + file;

                    progress.SetProgress(basicProgress);
                    if (this.cancelled)
                    {
                        break;
                    }

                    FtpWebResponse response = null;

                    while (!this.cancelled)
                    {

                        bool success = false;
                        Exception lastException = null;

                        for (int i = 0; i < 4 && !this.cancelled; i++)
                        {
                            try
                            {

                                progress.SetProgress(basicProgress + " - Connecting");

                                lock (FtpSite.LockInstance())
                                {
                                    string ftpfullpath = "ftp://" + host + targetfolder + "/" + file;
                                    FtpWebRequest ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);

                                    ftp.UseBinary = true;
                                    ftp.KeepAlive = true;
                                    ftp.Method = WebRequestMethods.Ftp.DeleteFile;
                                    ftp.Timeout = 2000;

                                    ftp.Credentials = new NetworkCredential(username, pwd);

                                    response = (FtpWebResponse)ftp.GetResponse();

                                    progress.SetProgress(basicProgress + response.StatusDescription );
                                    success = true;
                                }
                                break;
                            }
                            catch (Exception e)
                            {
                                lastException = e;

                            }
                            finally
                            {

                                try { if (response != null) response.Close(); }
                                catch (Exception) { }
                            }

                        }
                        if (this.cancelled)
                        {
                            return;
                        }

                        if (!success)
                        {
                            DialogResult keepGoing = MessageBox.Show("Keep Trying? Failed after 4 attempts to delete: "
                                                                     + file + " Exception: " + lastException.Message
                                                                    , "FTP Failure"
                                                                    , MessageBoxButtons.OKCancel
                                                                    , MessageBoxIcon.Error
                                                                    , MessageBoxDefaultButton.Button1);

                            if (keepGoing != DialogResult.OK)
                            {
                                return;  // Don't true any more.
                            }
                        }

                        if (success)
                        {
                            break;  // Goto next file only on success.
                        }
                    }

                }
              }
            }
            finally
            {
                progress.DoClose();
            }
        
    }

	}
}
