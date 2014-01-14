using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;


namespace Kexplorer
{
	/// <summary>
	/// Summary description for DriveLoader.
	/// </summary>
	public class FtpLoaderWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
		private FtpSite site;
		private ISimpleKexplorerGUI form;
		private KexplorerFtpNode createdNode = null;
        FtpWebRequest ftp = null; 
        
		//private DirectoryInfo driveInfo = null;
		private String[] subDirs = null;
        private String currentPath = null;

		private KexplorerFtpNode currentWorkingSubDir = null;
		private IWorkGUIFlagger guiFlagger = null;
		private bool stop = false;
		#endregion --------------------------------------------------------------------

		#region Constructors ------------------------------------------------------------
		/// <summary>
		/// Construct with the drive letter and a pointer to the main form
		/// </summary>
		/// <param name="newDriveLetter"></param>
		/// <param name="form1"></param>
		public FtpLoaderWorkUnit( KexplorerFtpNode newCreatedDriveNode, FtpSite site, ISimpleKexplorerGUI form1, IWorkGUIFlagger flagger )
		{
			this.createdNode = newCreatedDriveNode;
			this.site = site;
			this.form = form1;
			this.guiFlagger = flagger;

		}
		#endregion ----------------------------------------------------------------------

		#region IWorkUnit Members -------------------------------------------------------

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Do one of two jobs.  If first time, the createdNode will be null.  In this case,
		/// test to see if the Drive letter exists, if so, create a Treenode.
		/// 
		/// Secondly, if the createdNode already exists, then get a directory listing of folders.
		/// </summary>
		/// <returns></returns>
		public IWorkUnit DoJob()
		{
			IWorkUnit moreWork = null;
			if (this.ftp == null )
			{
				moreWork = this.DoJobOne();
			} 
			else 
			{
				moreWork = this.DoJobThree();
			}
			return moreWork;
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Soft about via flag.
		/// </summary>
		public void Abort()
		{
			this.stop = true;
		}



		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Upon notification of being aborted, set all related nodes to stale.
		/// </summary>
		public void YouWereAborted()
		{

			if ( this.createdNode != null )
			{
				this.createdNode.Stale = true;
			}
			

		}




		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------

		#region Job 1
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Job one is setup the ftp connection.
		/// 
		/// KCS: 2/19/08 - Drive nodes are created by the main controller, because we know we always
		/// want them.  This insures they're created in alphabetical order. etc.
		/// </summary>
		private IWorkUnit DoJobOne()
		{

            IWorkUnit nextWorkUnit = null;
			
			if ( this.ftp == null && !this.stop )
			{

                try
                {
                    ArrayList files = new ArrayList();
                    lock (FtpSite.LockInstance())
                    {
                        currentPath = site.targetFolder + "/";
                        string ftpfullpath = "ftp://" + site.host + currentPath;

                        ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);

                        ftp.Credentials = new NetworkCredential(site.username, site.pwd);
                        //userid and password for the ftp server to given  

                        ftp.KeepAlive = true;
                        ftp.UseBinary = true;

                        ftp.Method = WebRequestMethods.Ftp.ListDirectoryDetails;


                        FtpWebResponse ftpResponse = (FtpWebResponse)ftp.GetResponse();
                        Stream responseStream = ftpResponse.GetResponseStream();



                        string strFile;
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            while ((strFile = reader.ReadLine()) != null)
                            {
                                FtpFileInfo fInfo = new FtpFileInfo(strFile, site.type);
                                if (fInfo.isDir && fInfo.name != "." && fInfo.name != ".." )
                                {
                                    files.Add(fInfo.name);
                                }
                            }


                        }

                        ftpResponse.Close();
                    }


                    this.subDirs = (String[])files.ToArray(typeof(String));



                    if (this.subDirs != null && this.subDirs.Length > 0 && !this.stop)
                    {

                        try
                        {

                            this.guiFlagger.SignalBeginGUI();

                            this.form.MainForm.Invoke(new InvokeDelegate(this.AddDirectories));

                        }
                        finally
                        {

                            this.guiFlagger.SignalEndGUI();
                        }

                        // If there were sub-dirs, then, Job 3 should load each one.
                        nextWorkUnit = this;
                    }


                }
                catch (Exception e)
                {
                    MessageBox.Show("Exception setting up FTP: " + e.Message);

                }
				finally 
				{
					
				}
				


			}	
			return nextWorkUnit;
		}

        //-----------------------------------------------------------------------------//
        /// <summary>
        /// Called from BeginInvoke.  The Dir info is available, add as sub-nodes to 
        /// this drive letter.
        /// </summary>
        private void AddDirectories()
        {
            lock (this.form.TreeView1)
            {
                //KExplorerNode[] nodes = new KExplorerNode[ this.subDirs.Length ];
                ArrayList nodes = new ArrayList();
                foreach (String subDir in this.subDirs)
                {
                    //nodes[i] = new KExplorerNode( this.subDirs[i] );
                    if (subDir.StartsWith("System "))
                    {
                        continue;
                    }
                    nodes.Add(new KexplorerFtpNode(site, currentPath + subDir, subDir));

                }
                this.createdNode.Nodes.AddRange((TreeNode[])nodes.ToArray(typeof(TreeNode)));
            }
        }

		#endregion

		#region Job 3
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Job three is to load all directories beneath the first level of directories.
		/// After this, there is no more work created from this.
		/// </summary>
		/// <returns></returns>
		private IWorkUnit DoJobThree()
		{
			
			try 
			{


		        foreach  ( KexplorerFtpNode dirNode in this.createdNode.Nodes )
				{
					if ( this.stop )
					{
						break;
					}
					this.currentWorkingSubDir = dirNode;

					this.subDirs = null;
					try 
					{

						this.subDirs = GetSubDirs( dirNode.Path  );
					} 
					catch (Exception e )
					{
                        MessageBox.Show(e.Message);
					}

					if ( this.subDirs != null && this.subDirs.Length > 0 && !this.stop )
					{
						try 
						{
							this.guiFlagger.SignalBeginGUI();

							this.form.MainForm.Invoke( new InvokeDelegate( this.AddSubDirsToSubDir ));
						} 
						finally 
						{
							this.guiFlagger.SignalEndGUI();
						}
					}

				} 

			} 
			finally 
			{
				this.currentWorkingSubDir = null;
				this.subDirs = null;
			}

			return null;
		}


        private String[] GetSubDirs(string path)
        {

            
            string ftpfullpath = "ftp://" + site.host + path;
            ArrayList files = new ArrayList();
            lock (FtpSite.LockInstance())
            {

                ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);

                ftp.Credentials = new NetworkCredential(site.username, site.pwd);
                //userid and password for the ftp server to given  

                ftp.KeepAlive = true;
                ftp.UseBinary = true;

                ftp.Method = WebRequestMethods.Ftp.ListDirectoryDetails;


                FtpWebResponse ftpResponse = (FtpWebResponse)ftp.GetResponse();
                Stream responseStream = ftpResponse.GetResponseStream();



                string strFile;
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    while ((strFile = reader.ReadLine()) != null)
                    {
                        FtpFileInfo fInfo = new FtpFileInfo(strFile, site.type);
                        if (fInfo.isDir && fInfo.name != "." && fInfo.name != "..")
                        {
                            files.Add(fInfo.name);
                        }
                    }


                }

                ftpResponse.Close();
            }


            return (String[])files.ToArray(typeof(String));

        }

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Add the found sub-directories to the current sub dir.
		/// </summary>
		private void AddSubDirsToSubDir()
		{
			lock( this.form.TreeView1 )
			{
				KexplorerFtpNode[] nodes = new KexplorerFtpNode[ this.subDirs.Length ];

				for ( int i = 0; i < nodes.Length; i++ )
				{
                    nodes[i] = new KexplorerFtpNode(site, this.currentWorkingSubDir.Path + "/" + this.subDirs[i], this.subDirs[i]);
					nodes[i].Collapse();

				}
			
			
				this.currentWorkingSubDir.Nodes.AddRange( nodes );
			}
		}
		#endregion




		#endregion ----------------------------------------------------------------------
	}
}
