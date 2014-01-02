using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for ServicesPanel.
	/// </summary>
	public class ServicesPanel : System.Windows.Forms.UserControl, IServiceGUI, ISimpleKexplorerGUI
	{
	
		private Form mainForm = null;
		private System.Windows.Forms.DataGrid serviceGrid;
		private System.Windows.Forms.ContextMenu serviceGridMenu;
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public ServicesPanel(Form newMainForm)
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call

			this.mainForm = newMainForm;
		}

		public Form MainForm
		{
			get { return this.mainForm; }
			set { this.mainForm = value; }
		}

		public TreeView TreeView1
		{
			get { return null; }
		}

		public DataGridView DataGridView1
		{
			get { return null; }
		}


		public ContextMenu ServiceGridMenu
		{
			get { return this.serviceGridMenu; }
		}

        

		public ContextMenu DirTreeMenu
		{
			get { return null; }
		}

		public ContextMenuStrip FileGridMenuStrip
		{
			get { return null; }
		}

		public string WatchingForFolder
		{
			get { return null;; }
			set { }
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );

			this.manager.Close();
		}

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.serviceGrid = new System.Windows.Forms.DataGrid();
			this.serviceGridMenu = new System.Windows.Forms.ContextMenu();
			((System.ComponentModel.ISupportInitialize)(this.serviceGrid)).BeginInit();
			this.SuspendLayout();
			// 
			// serviceGrid
			// 
			this.serviceGrid.ContextMenu = this.serviceGridMenu;
			this.serviceGrid.DataMember = "";
			this.serviceGrid.Dock = System.Windows.Forms.DockStyle.Fill;
			this.serviceGrid.HeaderForeColor = System.Drawing.SystemColors.ControlText;
			this.serviceGrid.Location = new System.Drawing.Point(0, 0);
			this.serviceGrid.Name = "serviceGrid";
			this.serviceGrid.Size = new System.Drawing.Size(608, 336);
			this.serviceGrid.TabIndex = 0;
			// 
			// ServicesPanel
			// 
			this.Controls.Add(this.serviceGrid);
			this.Name = "ServicesPanel";
			this.Size = new System.Drawing.Size(608, 336);
			((System.ComponentModel.ISupportInitialize)(this.serviceGrid)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		public DataGrid ServiceGrid
		{
			get { return this.serviceGrid; }
		}


		private ServiceMgrWorkUnit manager = null;

		public ServiceMgrWorkUnit Manager 
		{
			get 
			{
				return this.manager;
			}
			set 
			{
				this.manager = value;
			}
		}
	}


}
