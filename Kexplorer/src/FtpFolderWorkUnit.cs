using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;

namespace Kexplorer
{

	/// <summary>
	/// A Unit of work for a Folder is two phazed.
	/// Load the sub-directories, then pass two each of their sub-dirs.
	/// </summary>
	public class FtpFolderWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
        private FtpSite site;
		private KexplorerFtpNode folderNode;
		private ISimpleKexplorerGUI form;
		private bool jobOneComplete = false;
		private String[] subDirs = null;
        private String currentPath = null;
		private KexplorerFtpNode currentSubFolderNode = null;
		private IWorkGUIFlagger guiFlagger = null;
		private bool stop = false;
        private FtpWebRequest ftp = null; 
		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Constructor with a node that is a folder.
		/// </summary>
		/// <param name="newFolderNode"></param>
		/// <param name="newForm"></param>
		public FtpFolderWorkUnit( KexplorerFtpNode newFolderNode, ISimpleKexplorerGUI newForm, IWorkGUIFlagger flagger )
		{
			this.folderNode = newFolderNode;
            this.site = newFolderNode.Site;
			this.form = newForm;
			this.guiFlagger = flagger;
		}
		#endregion ----------------------------------------------------------------------


		#region IWorkUnit Members -------------------------------------------------------

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Job One.  Initialize all sub-dirs.
		/// Job Two.  Initailize each additional sub-dir.
		/// </summary>
		/// <returns></returns>
		public IWorkUnit DoJob()
		{
			IWorkUnit moreWork = null;

			if ( !this.jobOneComplete )
			{
				moreWork = this.DoJobOne();
			} 
			else 
			{
				moreWork = this.DoJobTwo();
			}

			return moreWork;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Set a flag to stop ASAP.
		/// </summary>
		public void Abort()
		{
			this.stop = false;
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Upon notification of being aborted, set all related nodes to stale.
		/// </summary>
		public void YouWereAborted()
		{
				this.folderNode.Stale = true;

		}

		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------

		#region Job 1
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// job one, load the sub-dirs, and set as complete, if there are sub-dirs 
		/// return "this" to perform the Job 2 Pass.
		/// </summary>
		/// <returns></returns>
		private IWorkUnit DoJobOne()
		{
			IWorkUnit moreWork = null;
			try 
			{
				// See if we're refreshing.
				if ( this.folderNode.Nodes.Count > 0
					&& this.folderNode.Stale )
				{
				
					try 
					{
						this.guiFlagger.SignalBeginGUI();
						this.form.MainForm.Invoke( new InvokeDelegate( this.ClearNode ));
					} 
					finally 
					{
						this.guiFlagger.SignalEndGUI();
					}
					

					this.folderNode.Stale = false;
				
				}

				if ( this.folderNode.Nodes.Count == 0  && !this.stop )
				{

					this.subDirs = null;
					this.subDirs = GetSubDirs( this.folderNode.Path, 4 );
					if ( this.subDirs == null )
					{
						this.YouWereAborted();
					}

					if ( this.subDirs != null && this.subDirs.Length > 0 && !this.stop )
					{
						try 
						{
							this.guiFlagger.SignalBeginGUI();
							this.form.MainForm.Invoke( new InvokeDelegate( this.AddSubNodes));
						} 
						finally 
						{
							this.guiFlagger.SignalEndGUI();
						}

						// We did something, so there's more work to be done.
						moreWork = this;

					}

				} 
				else 
				{
					moreWork = this;
				}

			} 
			finally 
			{
				this.subDirs = null;
				this.jobOneComplete = true;
			}


			return moreWork;

		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Clear the sub-nodes of this node.  Separate method so it can be invoked under the
		/// Form's thread.
		/// </summary>
		private void ClearNode()
		{
			lock( this.form.TreeView1 )
			{
				this.folderNode.Nodes.Clear();
			}
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Add sub directories.
		/// </summary>
		private void AddSubNodes()
		{
			lock( this.form.TreeView1 )
			{

				KexplorerFtpNode[] nodes = new KexplorerFtpNode[ this.subDirs.Length ];

	
				for ( int i = 0; i < nodes.Length; i++ )
				{
					nodes[i] = new KexplorerFtpNode( site, this.folderNode.Path + "/" + this.subDirs[i], this.subDirs[i] );

				}
				this.folderNode.Nodes.AddRange( nodes );

			}
		}

		#endregion

		#region Job 2
		//-----------------------------------------------------------------------------//
		/// 6<summary>
		/// Go through each sub-folder and load it's sub-folders.
		/// </summary>
		/// <returns></returns>
		private IWorkUnit DoJobTwo()
		{
			try 
			{
				foreach ( KexplorerFtpNode sNode in this.folderNode.Nodes )
				{
					if ( this.stop )
					{
						break;
					}
					this.currentSubFolderNode = sNode;

					if ( sNode.Nodes.Count > 0 && sNode.Stale )
					{

						try 
						{
							this.guiFlagger.SignalBeginGUI();
							this.form.MainForm.Invoke( new InvokeDelegate( this.ClearSubNode ));
						} 
						finally 
						{
							this.guiFlagger.SignalEndGUI();
						}

						sNode.Stale = false;

					}

					if ( sNode.Nodes.Count == 0 )
					{
						try 
						{
							this.subDirs = null;
                            String nextPath = sNode.Path;
                            
							this.subDirs = GetSubDirs( nextPath, 4 );

							if ( this.subDirs == null )
							{
								this.YouWereAborted();
							}
							if ( this.subDirs != null && this.subDirs.Length > 0 && !this.stop )
							{
								try 
								{

									this.guiFlagger.SignalBeginGUI();
									this.form.MainForm.Invoke( new InvokeDelegate( this.AddSubSubNodes ));
								} 
								finally 
								{
									this.guiFlagger.SignalEndGUI();
								}
							}
						} 
						catch (Exception )
						{
                            continue;
						}

					}

				}

			} 
			finally 
			{
				this.subDirs = null;
				this.currentSubFolderNode = null;
			}



			return null;

		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// CAleld from form's invoke, clear a tree node's sub-nodes.
		/// </summary>
		private void ClearSubNode()
		{
			lock( this.form.TreeView1 )
			{
				this.currentSubFolderNode.Nodes.Clear();
			}
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Called from form's invoke, actual treeview operations.
		/// </summary>
		private void AddSubSubNodes()
		{
			lock( this.form.TreeView1 )
			{
				KexplorerFtpNode[] nodes = new KexplorerFtpNode[ this.subDirs.Length];

				for ( int i = 0; i < nodes.Length; i++ )
				{
                    nodes[i] = new KexplorerFtpNode(this.site, this.currentSubFolderNode.Path + "/" + this.subDirs[i], this.subDirs[i]);

				}
				this.currentSubFolderNode.Nodes.AddRange( nodes );


			}
		}


		#endregion


        private String[] GetSubDirs(string path, int countdown)
        {
            if (countdown == 0)
            {
                return new String[0];
            }
            if (path.Contains("HNI/5.4"))
            {
                String x = path;
                Console.WriteLine(x);
            }

            ArrayList files = new ArrayList();
            string ftpfullpath = "ftp://" + site.host + path;
            FtpWebResponse ftpResponse = null;
            try
            {

                    ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);


                    ftp.Credentials = new NetworkCredential(site.username, site.pwd);
                    //userid and password for the ftp server to given  

                    ftp.KeepAlive = true;
                    ftp.UseBinary = true;

                    ftp.Method = WebRequestMethods.Ftp.ListDirectoryDetails;



                    ftpResponse = (FtpWebResponse)ftp.GetResponse();


                Stream responseStream = null;


                    responseStream = ftpResponse.GetResponseStream();




                string strFile = null;

                try
                {
                    StreamReader reader = new StreamReader(responseStream);
                    while (true)
                    {
                        strFile = null;
                        try
                        {
                            strFile = reader.ReadLine();
                        }
                        catch (IOException e)
                        {
                            break;
                        }



                        if (strFile != null)
                        {
                            FtpFileInfo fInfo = new FtpFileInfo(strFile, site.type);
                            if (fInfo.isDir)
                            {
                                if (!(fInfo.name.Equals(".") || fInfo.name.Equals("..")))
                                {
                                    files.Add(fInfo.name);
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    String x = e.Message;
                    if (x.Contains("550"))
                    {

                        Thread.Sleep(200);
                        try
                        {
                            ftpResponse.Close();
                        }
                        catch (Exception) { }
                        return GetSubDirs(path, --countdown);

                    }
                }



                try
                {
                    ftpResponse.Close();
                }
                catch (Exception e)
                {
                }
            }

            catch (WebException e)
            {
                String x = e.Message;
                Console.WriteLine(x);
                Thread.Sleep(50);
                return null;
            }
            finally
            {
                if (ftpResponse != null)
                {
                    ftpResponse.Close();
                }
            }


            return (String[])files.ToArray(typeof(String));

        }


		#endregion ----------------------------------------------------------------------
	}
}
