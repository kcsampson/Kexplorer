using System;
using System.Collections;
using System.Data;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Kexplorer.services;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for TreeViewPersistState.
	/// </summary>
	public class TreeViewPersistState
	{

		private string[] drives = null;

		private string tabName = null;

		private string currentFolder = null;

		private bool isSelected = false;

		private ArrayList visibleServices = null;

        private FtpSite ftpSite = null;

		public TreeViewPersistState( ISimpleKexplorerGUI kexplorerPanel, string newTabName, bool newIsSelected )
		{

			this.tabName = newTabName;

			this.isSelected = newIsSelected;

			if ( kexplorerPanel is KexplorerPanel )
			{
				this.Initialize( kexplorerPanel );
			} 
			else if ( kexplorerPanel is ServicesPanel )
			{

				this.InitializeServicePanel( (ServicesPanel)kexplorerPanel );
			}


		}






		/// <summary>
		/// Read the tree state of the GUI to figure out what is loaded, where it pointed, whats
		/// the name of the tab.
		/// </summary>
		/// <param name="kexplorerTab"></param>
		private void Initialize(  ISimpleKexplorerGUI kexplorerTab )
		{

            if (kexplorerTab.TreeView1.Nodes[0] is KexplorerFtpNode)
            {
                var site =  (KexplorerFtpNode)kexplorerTab.TreeView1.Nodes[0];
                this.ftpSite = site.Site;

            }
            else
            {
                KExplorerNode selectedNode = (KExplorerNode)kexplorerTab.TreeView1.SelectedNode;

                if (selectedNode != null)
                {
                    this.currentFolder = selectedNode.DirInfo.FullName;
                }
                else
                {
                    this.currentFolder = "";
                }

                ArrayList tdrives = new ArrayList();

                foreach (KExplorerNode node in kexplorerTab.TreeView1.Nodes)
                {

                    tdrives.Add(node.Text);

                }

                this.drives = (string[])tdrives.ToArray(typeof(string));
            }
		}


		public void WriteXmlOutput( StringBuilder output )
		{
	

			output.Append("<KexplorerTab>\n");

			output.Append("<TabName>" + this.tabName + "</TabName>\n" );

			output.Append("<CurrentFolder>"+this.currentFolder+"</CurrentFolder>\n" );

			if ( this.isSelected )
			{
				output.Append("<Selected/>\n");
			}

			if ( this.visibleServices != null )
			{
				output.Append( "<ServicesTab/>\n");
				foreach ( ServiceController serviceController in this.visibleServices )
				{
					output.Append("<Service>"+serviceController.ServiceName+"//"+serviceController.MachineName + "</Service>\n" );
				}
			}  else if ( this.ftpSite != null ){
                output.Append("<FtpTab>");
                output.Append("<Host>" + this.ftpSite.host + "</Host>\n");
                output.Append("<UserName>" + this.ftpSite.username + "</UserName>\n");
                output.Append("<Pwd>" + this.ftpSite.pwd + "</Pwd>\n");
                output.Append("<Type>" + this.ftpSite.type + "</Type>\n");
                output.Append("<TargetFolder>" + this.ftpSite.targetFolder + "</TargetFolder>\n");
                output.Append("</FtpTab>\n");

            }
			else 
			{

				foreach ( string drive in this.drives )
				{
					output.Append("<Drive>"+drive+"</Drive>\n" );

				}
			}

			output.Append("</KexplorerTab>\n");
		}


		public void InitializeServicePanel(ServicesPanel servicesPanel)
		{
			DataView dView = (DataView) servicesPanel.ServiceGrid.DataSource;

			this.visibleServices = new ArrayList();
			foreach ( DataRow row in dView.Table.Rows )
			{

				this.visibleServices.Add( row["ServiceControllerObject"]);
			}
		}
	}
}
