using System;
using System.Collections;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for DriveLoader.
	/// </summary>
	public class DriveLoaderWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
		private string driveLetter;
		private ISimpleKexplorerGUI form;
		private KExplorerNode createdNode = null;
		private DirectoryInfo driveInfo = null;
		private DirectoryInfo[] subDirs = null;

		private KExplorerNode currentWorkingSubDir = null;
		private IWorkGUIFlagger guiFlagger = null;
		private bool stop = false;
		#endregion --------------------------------------------------------------------

		#region Constructors ------------------------------------------------------------
		/// <summary>
		/// Construct with the drive letter and a pointer to the main form
		/// </summary>
		/// <param name="newDriveLetter"></param>
		/// <param name="form1"></param>
		public DriveLoaderWorkUnit( KExplorerNode newCreatedDriveNode, string newDriveLetter, ISimpleKexplorerGUI form1, IWorkGUIFlagger flagger )
		{
			this.createdNode = newCreatedDriveNode;
			this.driveLetter = newDriveLetter;
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
			if (this.createdNode.DirInfo == null )
			{
				moreWork = this.DoJobOne();

			} 
			else if ( this.subDirs == null )
			{
				moreWork = this.DoJobTwo();
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
		/// Job one is create a treenode if this drive exists.
		/// 
		/// KCS: 2/19/08 - Drive nodes are created by the main controller, because we know we always
		/// want them.  This insures they're created in alphabetical order. etc.
		/// </summary>
		private IWorkUnit DoJobOne()
		{
			this.driveInfo = DirPerfStat.Instance().MakeDirectoryInfo( this.driveLetter + ":\\");

			IWorkUnit nextWorkUnit = null;
			if ( this.driveInfo.Exists && !this.stop )
			{

				try 
				{
					this.createdNode.DirInfo = this.driveInfo;
					//this.guiFlagger.SignalBeginGUI();
					//this.form.MainForm.Invoke( new InvokeDelegate( this.AddDriveLetter ) );

					// A treenode is now created.  This object can serve as a new
					// piece of work which will be to load the directories under it.
					nextWorkUnit = this;
				} 
				finally 
				{
					//this.guiFlagger.SignalEndGUI();
				}
				


			}	
			return nextWorkUnit;
		}


		#endregion

		#region Job 2

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Job two is to load the sub-directories.
		/// </summary>
		private IWorkUnit DoJobTwo()
		{
			IWorkUnit nextWorkUnit = null;
			try 
			{

				this.subDirs = DirPerfStat.Instance().GetDirectories( this.driveInfo );


				if ( this.subDirs != null && this.subDirs.Length > 0 && !this.stop )
				{

					try 
					{

						this.guiFlagger.SignalBeginGUI();

						this.form.MainForm.Invoke( new InvokeDelegate( this.AddDirectories));

					} 
					finally 
					{

						this.guiFlagger.SignalEndGUI();
					}

					// If there were sub-dirs, then, Job 3 should load each one.
					nextWorkUnit = this;
				}
			} 
			finally 
			{
				//this.subDirs = null;
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
			lock ( this.form.TreeView1 )
			{
				//KExplorerNode[] nodes = new KExplorerNode[ this.subDirs.Length ];
				ArrayList nodes = new ArrayList();
				foreach ( DirectoryInfo subDir in this.subDirs )
				{
					//nodes[i] = new KExplorerNode( this.subDirs[i] );
					if ( subDir.Name.StartsWith("System ") )
					{
						continue;
					}
					nodes.Add( new KExplorerNode( subDir ) );
			
				}
				this.createdNode.Nodes.AddRange( (TreeNode[])nodes.ToArray( typeof( TreeNode ) ) );
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


				foreach ( KExplorerNode dirNode in this.createdNode.Nodes )
				{
					if ( this.stop )
					{
						break;
					}
					this.currentWorkingSubDir = dirNode;

					this.subDirs = null;
					try 
					{

						this.subDirs = DirPerfStat.Instance().GetDirectories( dirNode.DirInfo );
					} 
					catch (Exception )
					{
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

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Add the found sub-directories to the current sub dir.
		/// </summary>
		private void AddSubDirsToSubDir()
		{
			lock( this.form.TreeView1 )
			{
				KExplorerNode[] nodes = new KExplorerNode[ this.subDirs.Length ];

				for ( int i = 0; i < nodes.Length; i++ )
				{
					nodes[i] = new KExplorerNode( this.subDirs[ i ] );
					nodes[i].Collapse();

				}
			
			
				this.currentWorkingSubDir.Nodes.AddRange( nodes );
			}
		}
		#endregion




		#endregion ----------------------------------------------------------------------
	}
}
