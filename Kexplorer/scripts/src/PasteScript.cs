using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using System.Linq;
using System.Net;
using Kexplorer.res;
using System.Threading;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Handle Paste functionality.  In the case of paste, it's both a folder and file script.
	/// </summary>
	public class PasteScript : BaseFileAndFolderScript, IFtpFolderScript
	{
        public static string LONG_NAME = "Edit - Paste";
		private bool copyOverFolderDialogOnce = false;

        SimpleProgress progress = null;
		public PasteScript()
		{

			this.Description = "Paste Cut/Copied files/folder to selected folder.";

			this.LongName = LONG_NAME;

			this.Active = false;

			this.ScriptShortCut = Shortcut.CtrlV;



		}

        public void Run(KexplorerFtpNode folder)
        {
            if (this.ScriptHelper.VARS["CUTFILES"] != null)
            {
                MessageBox.Show("Cut to FTP Not Supported", "Kexplorer");
                return;
            }
            ScriptRunParams runParams = (ScriptRunParams)
									this.ScriptHelper.VARS["COPYFILES"];

            if (runParams != null && runParams.FtpNode != null)
            {
                MessageBox.Show("FTP to FTP transfer not supported.", "Kexplorer");
            }
            else if (runParams.Files == null || runParams.Files.Count() == 0)
            {
                MessageBox.Show("FTP Upload only supported for file lists, not a folder.");
            }
            else
            {
                this.UploadFtpFiles(folder, runParams.FolderNode, runParams.Files);
            }
        }

		/// <summary>
		/// Allows pasting into the Files grid, but, ignores which file it's pasted on and
		/// goes to the folder.
		/// </summary>
		/// <param name="folder"></param>
		/// <param name="file"></param>
		public override void Run(KExplorerNode folder, FileInfo[] file)
		{
			this.Run( folder );
		}

		public override void Run(KExplorerNode folder)
		{

			ScriptRunParams runParams = (ScriptRunParams)
									this.ScriptHelper.VARS["COPYFILES"];

			if ( runParams != null && runParams.FtpNode != null ){
                this.DownloadFtpFiles(folder, runParams.FtpNode, runParams.FtpFiles);
            } else if ( runParams != null )
			{
				KExplorerNode parentNode = (KExplorerNode)runParams.FolderNode.Parent;

				if ( parentNode != null )
				{
					if ( parentNode.DirInfo.FullName.Equals( folder.DirInfo.FullName ) )
					{
						MessageBox.Show(
							"Can't paste over top.  Use 'Backup Copy Here' option."
							, "Past Error"
							, MessageBoxButtons.OK );
						return;
					}
				}
				this.CopyFiles( folder, runParams.FolderNode, runParams.Files );



			} else {

				runParams = (ScriptRunParams)this.ScriptHelper.VARS["CUTFILES"];
                if (runParams != null && runParams.FtpNode != null)
                {
                    MessageBox.Show("Cut not supported from FTP", "Kexplorer");
                    return;
                   // this.DownloadFtpFiles(folder, runParams.FtpNode, runParams.FtpFiles, true);
                }
				else if ( runParams != null )
				{
					KExplorerNode parentNode = (KExplorerNode)runParams.FolderNode.Parent;

					if ( parentNode != null )
					{
						if ( parentNode.DirInfo.FullName.Equals( folder.DirInfo.FullName ) )
						{
							MessageBox.Show(
								"Can't paste over top.  Use 'Backup Copy Here' option."
								, "Past Error"
								, MessageBoxButtons.OK );
							return;
						}
					}

					this.MoveFiles( folder, runParams.FolderNode, runParams.Files );

					this.Active = false;

				}
			}

			this.ScriptHelper.RefreshFolder( folder, false );


		}


        

        private bool cancelled = false;

        private void FtpCancelled()
        {
            this.cancelled = true;
        }



        private KExplorerNode destNode;
        private KexplorerFtpNode sourceNode;
        private string[] files;
        private Thread workerThread = null;
        

        private void StartFtpDownLoadInThread()
        {
            try
            {
                Thread.Sleep(50);

                String host = sourceNode.Site.host;
                String username = sourceNode.Site.username;
                String pwd = sourceNode.Site.pwd;

                String targetfolder = sourceNode.Path;

                int count = files.Count();
                int iFileCount = 0;

                foreach (string file in files)
                {
                    string basicProgress = (++iFileCount).ToString() + " of " + count.ToString() + " : " + file;
                    progress.SetProgress(basicProgress);
                    if (this.cancelled)
                    {
                        break;
                    }


                    FtpWebResponse response = null;
                    FileStream outputStream = null;
                    Stream ftpStream = null;


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
                                    ftp.Method = WebRequestMethods.Ftp.DownloadFile;
                                    ftp.Timeout = 2000;

                                    ftp.Credentials = new NetworkCredential(username, pwd);

                                    response = (FtpWebResponse)ftp.GetResponse();

                                    outputStream = new FileStream(Path.Combine(destNode.FullPath, file), FileMode.Create);

                                    ftpStream = response.GetResponseStream();

                                    progress.SetProgress(basicProgress + " - Reponse Recieved");

                                    long cl = response.ContentLength;
                                    int bufferSize = 2048 * 2 * 2 * 2 * 2 * 2 * 2;
                                    int readCount;
                                    int totalCount = 0;
                                    byte[] buffer = new byte[bufferSize];

                                    readCount = ftpStream.Read(buffer, 0, bufferSize);
                                    while (readCount > 0 & !this.cancelled)
                                    {
                                        totalCount += readCount;
                                        progress.SetProgress(basicProgress + " - Downloading: " + totalCount.ToString()
                                                        );

                                        outputStream.Write(buffer, 0, readCount);
                                        readCount = ftpStream.Read(buffer, 0, bufferSize);
                                    }
                                    success = true;
                                    progress.SetProgress(basicProgress + " - Completed");
                                }
                                break;
                            }
                            catch (Exception e)
                            {
                                lastException = e;

                            }
                            finally
                            {

                                try { if (ftpStream != null) ftpStream.Close(); }
                                catch (Exception) { }

                                try { if (outputStream != null) outputStream.Close(); }
                                catch (Exception) { }

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
                            DialogResult keepGoing = MessageBox.Show("Keep Trying? Failed after 4 attempts to download: "
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

                        if (success  )
                        {

                            break;  // Goto next file only on success.
                        }
                    }


                }
            }
            finally
            {
                progress.DoClose();
            }

        }


        private void DownloadFtpFiles(KExplorerNode xdestNode
                               , KexplorerFtpNode xsourceNode
                               , string[] xfiles
                                 )
        {
            
            destNode = xdestNode;
            sourceNode = xsourceNode;
            files = xfiles;
            this.workerThread = new Thread(new ThreadStart(this.StartFtpDownLoadInThread));
            this.workerThread.Start();
            this.cancelled = false;


            progress = SimpleProgress.StartProgress(this.FtpCancelled
                                            , "Kexplorer FTP Download"
                                            , "Starting...");



            while (!this.cancelled && this.workerThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(250);
            }

        }

        KexplorerFtpNode ftpDestNode = null;
        KExplorerNode folderSourceNode = null;
        FileInfo[] sourceFiles = null;

        private void UploadFtpFiles( KexplorerFtpNode destNode, KExplorerNode sourceNode, FileInfo[] files ){
            ftpDestNode = destNode;
            folderSourceNode = sourceNode;
            sourceFiles = files;
            this.workerThread = new Thread(new ThreadStart(this.StartFtpUploadInThread));
            this.workerThread.Start();
            this.cancelled = false;


            progress = SimpleProgress.StartProgress(this.FtpCancelled
                                            , "Kexplorer FTP Upload"
                                            , "Starting...");



            while (!this.cancelled && this.workerThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(250);
            }

            
            
        }


        private void StartFtpUploadInThread()
        {
            try
            {
                Thread.Sleep(50);

                String host = ftpDestNode.Site.host;
                String username = ftpDestNode.Site.username;
                String pwd = ftpDestNode.Site.pwd;

                

                String targetfolder = ftpDestNode.Path;

              if ( sourceFiles != null ){
                int count = sourceFiles.Count();
                int iFileCount = 0;


                foreach (var file in sourceFiles)
                {
                    string basicProgress = (++iFileCount).ToString() + " of " + count.ToString() + " : " + file.Name;

                    progress.SetProgress(basicProgress);
                    if (this.cancelled)
                    {
                        break;
                    }

                    FtpWebResponse response = null;
                    FileStream inputStream = null;
                    Stream ftpStream = null;

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
                                    string ftpfullpath = "ftp://" + host + targetfolder + "/" + file.Name;
                                    FtpWebRequest ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);

                                    ftp.UseBinary = true;
                                    ftp.KeepAlive = true;
                                    ftp.Method = WebRequestMethods.Ftp.UploadFile;
                                    ftp.Timeout = 2000;

                                    ftp.Credentials = new NetworkCredential(username, pwd);

                                    response = (FtpWebResponse)ftp.GetResponse();

                                                        
                                    inputStream = File.OpenRead(file.FullName);  
 
                                   
                                  //  ftpStream = response.GetResponseStream();  
                                    ftpStream = ftp.GetRequestStream();


                                    progress.SetProgress(basicProgress + " - Reponse Recieved");

                                   
                                    int bufferSize = 2048 * 2 * 2 * 2 * 2 * 2 * 2;
                                    int readCount;
                                    int totalCount = 0;
                                    byte[] buffer = new byte[bufferSize];

                                    readCount = inputStream.Read(buffer, 0, bufferSize);
                                    while (readCount > 0 & !this.cancelled)
                                    {
                                        totalCount += readCount;
                                        progress.SetProgress(basicProgress + " - Uploading: " + totalCount.ToString()
                                                        );

                                        ftpStream.Write(buffer, 0, readCount);
                                        readCount = inputStream.Read(buffer, 0, bufferSize);
                                    }
                                    success = true;
                                    progress.SetProgress(basicProgress + " - Completed");
                                }
                                break;
                            }
                            catch (Exception e)
                            {
                                lastException = e;

                            }
                            finally
                            {

                                try { if (ftpStream != null) ftpStream.Close(); }
                                catch (Exception) { }

                                try { if (inputStream != null) inputStream.Close(); }
                                catch (Exception) { }

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
                            DialogResult keepGoing = MessageBox.Show("Keep Trying? Failed after 4 attempts to download: "
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
 

		private void CopyFiles( KExplorerNode destNode
							, KExplorerNode sourceNode
							, FileInfo[] files )
		{

			bool askOnce = false;
			if ( files != null )
			{
				foreach ( FileInfo file in files )
				{
					Console.WriteLine( "Would be copying...");
					Console.WriteLine( file.FullName);
					string temp = "TO: " +destNode.DirInfo.FullName + "\\"+file.Name;
					Console.WriteLine(  temp );
					if ( File.Exists( destNode.DirInfo.FullName + "\\"
						+	file.Name ) )
					{
						if ( !askOnce )
						{
						
							DialogResult result = MessageBox.Show( "File: " + file.Name + " already exists. Overwrite?"
								, "Overwrite File"
								, MessageBoxButtons.YesNoCancel
								, MessageBoxIcon.Exclamation
								, MessageBoxDefaultButton.Button3 );

							if ( result == DialogResult.Cancel )
							{
								return;
							}

							if ( result == DialogResult.Yes )
							{
								askOnce = true;
							}

							if ( result == DialogResult.No )
							{
								continue;
							}


						}

						File.Delete(destNode.DirInfo.FullName + "\\"
							+	file.Name );

					}


					File.Copy(  file.FullName
						,	destNode.DirInfo.FullName + "\\"
						+	file.Name );
				}
			}  
			else 
			{
				this.copyOverFolderDialogOnce = false;
				this.DuplicateFolder( sourceNode.DirInfo, destNode.DirInfo );

			

			}

		}


		private void DuplicateFolder( DirectoryInfo sourceDir, DirectoryInfo targetDir )
		{

			// Copy entire folder?  Wow... Tough...  But okay..
			if ( Directory.Exists( targetDir.FullName + "\\" + sourceDir.Name ) )
			{
				if ( !this.copyOverFolderDialogOnce )
				{
					DialogResult result = MessageBox.Show( "Folder: " + sourceDir.Name
						+ " already exists. Copy Over?"
						, "Overwrite Folder"
						, MessageBoxButtons.YesNo
						, MessageBoxIcon.Exclamation
						, MessageBoxDefaultButton.Button2 );


					if ( result == DialogResult.Yes )
					{
						this.copyOverFolderDialogOnce = true;
					}

					if ( result == DialogResult.No )
					{
						return;
					}
				}
			} 
			else 
			{
				Directory.CreateDirectory( targetDir.FullName + "\\" + sourceDir.Name );
			}
			foreach ( FileInfo file in sourceDir.GetFiles() )
			{
				string targetFile = targetDir.FullName 
					+ "\\"
					+ sourceDir.Name
					+ "\\"
					+ file.Name;

				if ( File.Exists( targetFile  ) )
				{
					File.SetAttributes( targetFile, FileAttributes.Normal );
				}

				file.CopyTo( targetFile, true  );

			}

			DirectoryInfo subTarget = new DirectoryInfo( targetDir.FullName + "\\" + sourceDir.Name );

			foreach ( DirectoryInfo dir in sourceDir.GetDirectories())
			{

				this.DuplicateFolder( dir, subTarget );
				
			}
		}
		


		private void MoveFiles( KExplorerNode destNode
			, KExplorerNode sourceNode
			, FileInfo[] files )
		{
			bool askOnce = false;
			if ( files != null )
			{
				foreach ( FileInfo file in files )
				{
					string targetFile = destNode.DirInfo.FullName + "\\"
						+	file.Name;
					if ( File.Exists( targetFile  ) )
					{
						if ( !askOnce )
						{
						
							DialogResult result = MessageBox.Show( "File: " + file.Name + " already exists. Overwrite?"
								, "Overwrite File"
								, MessageBoxButtons.YesNoCancel
								, MessageBoxIcon.Exclamation
								, MessageBoxDefaultButton.Button3 );

							if ( result == DialogResult.Cancel )
							{
								return;
							}

							if ( result == DialogResult.Yes )
							{
								askOnce = true;
							}

							if ( result == DialogResult.No )
							{
								continue;
							}					


						}
						File.SetAttributes( targetFile, FileAttributes.Normal );
						File.Delete( targetFile );
					}
					System.IO.File.Move(  file.FullName
						,	targetFile );
					
				}
			} else 
			{
				KExplorerNode parent = (KExplorerNode) sourceNode.Parent;
				string x = destNode.FullPath + "\\" + sourceNode.DirInfo.Name;
				sourceNode.DirInfo.MoveTo( x );

				this.ScriptHelper.RefreshFolder( parent, true);
				this.ScriptHelper.RefreshFolder( destNode, true );
			}
		}
	}
}
