using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for KexplorerPanel.
	/// </summary>
	public class KexplorerPanel : System.Windows.Forms.UserControl, ISimpleKexplorerGUI
	{
		private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.ContextMenu dirTreeMenu;
		private System.Windows.Forms.ContextMenuStrip fileGridMenuStrip;

		private bool initialized = false;

		private Form mainForm = null;

        private KExplorerControl controller;
        private DataGridTextBoxColumn dataGridTextBoxColumn2;
        private DataGridTextBoxColumn dataGridTextBoxColumn1;
        private DataGridView dataGridView1;
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public KexplorerPanel( Form newMainForm )
		{
			this.mainForm = newMainForm;
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call

			this.controller = new KExplorerControl();

			// Lazy handled by the tab page manager.
			this.controller.Initialize( this );
			//this.InitializeOnce();



		}


		public void InitializeOnce ()
		{
			

		}

		public KexplorerPanel( Form newMainForm, string currentFolderName, ArrayList drives )
		{
			this.mainForm = newMainForm;
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call

			this.controller = new KExplorerControl();

			this.controller.Initialize( this, currentFolderName, drives );



		}

        public KexplorerPanel(Form newMainForm, FtpSite ftpSite)
        {
            this.mainForm = newMainForm;
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            // TODO: Add any initialization after the InitializeComponent call

            this.controller = new KExplorerControl();

            this.controller.Initialize(this, ftpSite);



        }
        

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if ( this.controller != null )
				{
					this.controller.Close();
				}
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.dirTreeMenu = new System.Windows.Forms.ContextMenu();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.fileGridMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            this.dataGridTextBoxColumn2 = new System.Windows.Forms.DataGridTextBoxColumn();
            this.dataGridTextBoxColumn1 = new System.Windows.Forms.DataGridTextBoxColumn();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // treeView1
            // 
            this.treeView1.ContextMenu = this.dirTreeMenu;
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Left;
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(288, 560);
            this.treeView1.TabIndex = 0;
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(288, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(3, 560);
            this.splitter1.TabIndex = 1;
            this.splitter1.TabStop = false;
            // 
            // dataGridTextBoxColumn2
            // 
            this.dataGridTextBoxColumn2.Format = "";
            this.dataGridTextBoxColumn2.FormatInfo = null;
            this.dataGridTextBoxColumn2.MappingName = "ServiceControllObject";
            this.dataGridTextBoxColumn2.Width = 0;
            // 
            // dataGridTextBoxColumn1
            // 
            this.dataGridTextBoxColumn1.Format = "";
            this.dataGridTextBoxColumn1.FormatInfo = null;
            this.dataGridTextBoxColumn1.MappingName = "Name";
            this.dataGridTextBoxColumn1.Width = 120;
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(291, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(501, 560);
            this.dataGridView1.TabIndex = 2;
            // 
            // KexplorerPanel
            // 
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.treeView1);
            this.Name = "KexplorerPanel";
            this.Size = new System.Drawing.Size(792, 560);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

		}
		#endregion

		public Form MainForm
		{
			get { return this.mainForm; }
				set { this.mainForm = value; }
		}

		public TreeView TreeView1
		{
			get { return this.treeView1; }
		}

		public DataGridView DataGridView1
		{
			get { return this.dataGridView1; }
		}

		public ContextMenu DirTreeMenu
		{
			get { return this.dirTreeMenu; }
		}

		public ContextMenuStrip FileGridMenuStrip
		{
            get { return this.fileGridMenuStrip; }
		}


		private string watchingForFolder = null;
		public string WatchingForFolder
		{
			get { return this.watchingForFolder; }
			set { this.watchingForFolder = value; }
		}

	}
}
