using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer
{
	[Serializable]
	public class KExplorerNode : TreeNode 
	{
		#region Member Variables --------------------------------------------------------
		private DirectoryInfo di = null;

		//private String driveLetter = null;
		
		private bool loaded = false;
		private bool stale = false;

		
		#endregion ----------------------------------------------------------------------

		#region Constructors, either a DirectoryInfo, or, a FileInfo --------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Use to construct folders.
		/// </summary>
		/// <param name="x"></param>
		public KExplorerNode( DirectoryInfo x )
		{
			this.di = x;
			this.Text = x.Name;
		}

		public KExplorerNode( string driveLetter )
		{
			this.Text = driveLetter + ":\\";
		}

		#endregion

		#region Public methods ----------------------------------------------------------

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// LoadChild nodes until the recurseCountdown reaches zero.
		/// </summary>
		/// <param name="recurseCountdown"></param>
		public void LoadNode( int recurseCountdown )
		{
			if ( recurseCountdown <= 0 )
			{
				return;
			}
			--recurseCountdown;

			if ( !this.loaded )
			{
				this.loaded = true;

				try 
				{
					DirectoryInfo[] subdirs = this.di.GetDirectories();
					foreach ( DirectoryInfo subdir in subdirs )
					{
						KExplorerNode subNode =  new KExplorerNode(  subdir );
						this.Nodes.Add( subNode );
						subNode.LoadNode( recurseCountdown );
						
					}			
				} 
				catch (Exception  )
				{
					
				}




			} 
			else 
			{

				foreach ( KExplorerNode node in this.Nodes )
				{
					node.LoadNode( recurseCountdown );
				}
			}


			
		}

		#endregion ----------------------------------------------------------------------

		#region Properties --------------------------------------------------------------
		/// <summary>
		/// Gives the directory info it points to.
		/// </summary>
		public DirectoryInfo DirInfo 
		{
			get { return this.di; }
			set { this.di = value; }
		}


		/// <summary>
		/// Sets whether or not this object is stale and we should rebuild sub-nodes next time
		/// we process it.
		/// Sets all children stale.
		/// </summary>
		public bool Stale 
		{
			get 
			{
				return this.stale;
			}
			set 
			{ 
				this.stale = value;
				if ( this.stale )
				{
					foreach ( KExplorerNode kNode in this.Nodes )
					{
						kNode.Stale = true;
					}
				}
			}
		}


		#endregion ----------------------------------------------------------------------

		
	}
}
