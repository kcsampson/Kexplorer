using System;
using System.IO;

namespace Kexplorer
{

	/// <summary>
	/// A Unit of work for a Folder is two phazed.
	/// Load the sub-directories, then pass two each of their sub-dirs.
	/// </summary>
	public class FolderWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
		private KExplorerNode folderNode;
		private ISimpleKexplorerGUI form;
		private bool jobOneComplete = false;
		private DirectoryInfo[] subDirs = null;
		private KExplorerNode currentSubFolderNode = null;
		private IWorkGUIFlagger guiFlagger = null;
		private bool stop = false;
		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Constructor with a node that is a folder.
		/// </summary>
		/// <param name="newFolderNode"></param>
		/// <param name="newForm"></param>
		public FolderWorkUnit( KExplorerNode newFolderNode, ISimpleKexplorerGUI newForm, IWorkGUIFlagger flagger )
		{
			this.folderNode = newFolderNode;
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
					this.subDirs = DirPerfStat.Instance().GetDirectories( this.folderNode.DirInfo );
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

				KExplorerNode[] nodes = new KExplorerNode[ this.subDirs.Length ];

	
				for ( int i = 0; i < nodes.Length; i++ )
				{
					nodes[i] = new KExplorerNode( this.subDirs[i] );

				}
				this.folderNode.Nodes.AddRange( nodes );

			}
		}

		#endregion

		#region Job 2
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Go through each sub-folder and load it's sub-folders.
		/// </summary>
		/// <returns></returns>
		private IWorkUnit DoJobTwo()
		{
			try 
			{
				foreach ( KExplorerNode sNode in this.folderNode.Nodes )
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
							this.subDirs = DirPerfStat.Instance().GetDirectories( sNode.DirInfo );

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
						catch (Exception)
						{
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
				KExplorerNode[] nodes = new KExplorerNode[ this.subDirs.Length];

				for ( int i = 0; i < nodes.Length; i++ )
				{
					nodes[i] = new KExplorerNode( this.subDirs[i] );

				}
				this.currentSubFolderNode.Nodes.AddRange( nodes );


			}
		}


		#endregion
		#endregion ----------------------------------------------------------------------
	}
}
