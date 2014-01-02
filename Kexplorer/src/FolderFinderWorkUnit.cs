using System;
using System.Windows.Forms;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for FolderFinderWorkUnit.
	/// </summary>
	public class FolderFinderWorkUnit : IWorkUnit
	{
		private string pathToLookFor = null;
		private KExplorerNode[] startNodes = null;
		private ISimpleKexplorerGUI form = null;
		private IKExplorerControl controller = null;

		private KExplorerNode expandThisNode = null;

		private bool stopper = false;
		public FolderFinderWorkUnit( string newPathToLookFor,KExplorerNode[] newStartNodes, ISimpleKexplorerGUI newForm, IKExplorerControl newFlagger  )
		{

			this.pathToLookFor = newPathToLookFor;

			this.startNodes = newStartNodes;

			this.form = newForm;

			this.controller = newFlagger;
			
		}

		public IWorkUnit DoJob()
		{
			IWorkUnit moreWork = null;
			KExplorerNode[] tempNodes = this.startNodes;
			foreach (KExplorerNode node in tempNodes )
			{
				if ( node.DirInfo != null && this.pathToLookFor.Equals( node.DirInfo.FullName ) )
				{
					
					this.expandThisNode = node;
					this.form.MainForm.Invoke( new InvokeDelegate( this.ExpandNode ));
					break;
				}
				else if ( node.DirInfo != null && this.pathToLookFor.StartsWith( node.DirInfo.FullName ))
				{

					///  The node may or may not be loaded.  If not, make sure to at least expand
					///  out to here.  Then that will cause another job to get loaded to load it's
					///  sub nodes.  Then, we just re-add this task.
					if ( node.Nodes.Count == 0 )
					{

						this.expandThisNode = node;

						this.form.MainForm.Invoke(new InvokeDelegate( this.ExpandNode ));



						Pipeline drivePipeline = (Pipeline) this.controller.DrivePipelines[this.pathToLookFor.Substring(0,1)];

						drivePipeline.AddJob( new FolderWorkUnit( node, this.form, (IWorkGUIFlagger)this.controller ));
						
						moreWork = this;
					} 
					else 
					{
						/// If the sub-nodes are already created.  We re-add this job, but set it
						/// to look a the sub-nodes.
						KExplorerNode[] nextStartNodes = new KExplorerNode[ node.Nodes.Count ];
						for (int i = 0; i < node.Nodes.Count; i++ )
						{

							nextStartNodes[i] = (KExplorerNode)node.Nodes[ i ];
						}
						this.startNodes = nextStartNodes;
						moreWork = this;

					}

					break;
				}
			}
			return moreWork;
		}


		private void ExpandNode()
		{
            try
            {
                lock (this.form.TreeView1)
                {
                    string name = this.expandThisNode.Text;
                    if (this.expandThisNode.DirInfo != null)
                    {
                        name = this.expandThisNode.FullPath;
                    }

                    this.expandThisNode.Expand();

                    this.form.TreeView1.SelectedNode = this.expandThisNode;





                }
            }
            catch (Exception e)
            {
            }
		}

		public void Abort()
		{
			this.stopper = true;
		}

		public void YouWereAborted()
		{
			
		}
	}
}
