using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using Kexplorer.scripts;
using System.Linq;

namespace Kexplorer
{
	/// <summary>
	/// Kontroller for the Kexplorer
	/// </summary>
	public class KExplorerControl : IWorkGUIFlagger, IKExplorerControl, IComparer
	{

		#region Member Variables --------------------------------------------------------
		private ISimpleKexplorerGUI form = null;
		private Pipeline pipeline = null;

		private Hashtable drivePipelines = null;

		private bool isGuiBeingChanged = false;

		private Launcher launcher = null;

		private IScriptMgr scriptManager = null;

		private static IScriptMgr singleScriptManager = null;


		// Sometime treeview menu event happen without the SelectedNode of the treeview
		// being the node nearest to where the user clicked.  So for menu item we
		// may use this one if not null.
		private KExplorerNode clickedNode = null;

        private Boolean isFtpSite = false;


		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Simple Constructor
		/// </summary>
		public KExplorerControl()
		{


		}
		#endregion ----------------------------------------------------------------------

		#region Public Methods ----------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Notice to shutdown any background threading etc.
		/// </summary>
		public void Close()
		{
			this.pipeline.StopWork();

			foreach ( Pipeline drivePipeline in this.drivePipelines.Values )
			{
				drivePipeline.StopWork();
			}
		}


		public void Initialize(ISimpleKexplorerGUI newForm)
		{
			this.Initialize( newForm, null, null );
		}


        public void Initialize(ISimpleKexplorerGUI newForm, FtpSite ftpSite)
        {
            this.isFtpSite = true;
            this.form = newForm;

            this.pipeline = new Pipeline(this.form);
            this.pipeline.StartWork();

           this.drivePipelines = new Hashtable();

            Pipeline drivePipeline = new Pipeline(this.form);
            this.drivePipelines[ftpSite.host] = drivePipeline;
            drivePipeline.StartWork();

            KexplorerFtpNode createdNode = new KexplorerFtpNode(ftpSite);
            this.form.TreeView1.Nodes.Add(createdNode);

            drivePipeline.AddJob(new FtpLoaderWorkUnit(createdNode, ftpSite, this.form, this));

            this.form.TreeView1.ContextMenu.Popup += new EventHandler(this.ContextMenu_Popup);

            this.form.TreeView1.AfterExpand += new TreeViewEventHandler(TreeView1_AfterExpand);


            this.form.TreeView1.KeyDown += new KeyEventHandler(TreeView1_KeyDown);


            this.form.TreeView1.AfterSelect += new TreeViewEventHandler(TreeView1_AfterFtpSelect);

  
            this.InitializeScriptManager();

        }



		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Initialize with a Form1
		/// </summary>
		/// <param name="newForm"></param>
		public void Initialize(ISimpleKexplorerGUI newForm, string currentFolderName, ArrayList onlyTheseDrives )
		{
			this.form = newForm;

			this.pipeline = new Pipeline( this.form);
			this.pipeline.StartWork();

			this.drivePipelines = new Hashtable();

			newForm.WatchingForFolder = currentFolderName;


			string[] drives = null;
			
			if ( onlyTheseDrives != null && onlyTheseDrives.Count > 0 )
			{
				drives = (string[])onlyTheseDrives.ToArray(typeof(string));
			} 
			else 
			{
				drives = System.IO.Directory.GetLogicalDrives();
			}

			foreach( string drive in drives )
			{
				//this.pipeline.AddJob( 
				//	new DriveLoaderWorkUnit( drive.Substring( 0, 1), this.form, this ) );

				Pipeline drivePipeline = new Pipeline( this.form );
				this.drivePipelines[ drive.Substring(0,1) ] = drivePipeline;
				drivePipeline.StartWork();

				KExplorerNode createdNode = new KExplorerNode( drive.Substring( 0, 1) );
				this.form.TreeView1.Nodes.Add( createdNode );

				drivePipeline.AddJob(new DriveLoaderWorkUnit( createdNode, drive.Substring( 0, 1), this.form, this ) );

			}

			this.launcher = new Launcher();

			this.launcher.Initialize();
			

			this.form.TreeView1.AfterExpand += new TreeViewEventHandler(TreeView1_AfterExpand);


			this.form.TreeView1.KeyDown += new KeyEventHandler(TreeView1_KeyDown);


			this.form.TreeView1.AfterSelect += new TreeViewEventHandler(TreeView1_AfterSelect);


			this.form.DataGridView1.KeyDown += new KeyEventHandler(DataGrid1_KeyDown);

			this.form.TreeView1.DoubleClick += new EventHandler(TreeView1_DoubleClick);

			this.form.DataGridView1.DoubleClick += new EventHandler(DataGrid1_DoubleClick);



			this.form.TreeView1.MouseDown += new MouseEventHandler(TreeView1_MouseDown);


			this.InitializeScriptManager();



            this.form.TreeView1.ContextMenu.Popup +=new EventHandler(ContextMenu_Popup );

            this.form.DataGridView1.ContextMenuStrip = this.form.FileGridMenuStrip;
            this.form.DataGridView1.ContextMenuStrip.Opening +=new System.ComponentModel.CancelEventHandler(ContextMenuStrip_Opening);
		

			if ( currentFolderName != null && currentFolderName.Length > 0 )
			{
				KExplorerNode driveNode = null;
				foreach ( KExplorerNode node in this.form.TreeView1.Nodes )
				{
					if ( currentFolderName.StartsWith( node.Text ))
					{
						driveNode = node;
						break;
					}
				}
				Pipeline drivePipeline = (Pipeline)this.drivePipelines[currentFolderName.Substring(0,1)];

				drivePipeline.AddJob( new FolderFinderWorkUnit(
					currentFolderName, new KExplorerNode[]{driveNode}
										, this.form
										, this
													));
				
			}

		}



		#endregion ----------------------------------------------------------------------

		#region Public Properties -------------------------------------------------------
		/// <summary>
		/// Allows other objects to put things on the pipeline.
		/// </summary>
		public Pipeline MainPipeLine 
		{
			get { return this.pipeline; }
		}


		/// <summary>
		/// Hash table key is drive letter.  Value is a Pipeline.
		/// </summary>
		public Hashtable DrivePipelines 
		{
			get { return this.drivePipelines; }
		}
		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------
		/// <summary>
		/// Initialize the script manager.
		/// Only for Files and Folders.  Not Services.
		/// </summary>
		private void InitializeScriptManager()
		{

			if ( KExplorerControl.singleScriptManager == null )
			{

				Assembly x = this.GetType().Assembly;

                this.scriptManager = new ScriptMgr();

				foreach ( Module module in x.GetModules() )
				{
				
					foreach ( Type type in module.GetTypes() )
					{
						IScript script = null;
						if ( type.IsClass && !type.IsAbstract )
						{
							if ( type.GetInterface("IFolderScript" ) != null )
							{
							
								script = (IScript)type.GetConstructor(new Type[0]).Invoke(new object[0] );
                                this.scriptManager.FolderScripts.Add((IFolderScript)script);
							}

							if ( type.GetInterface("IFileScript" ) != null )
							{
								if ( script == null )
								{
									script = (IScript)type.GetConstructor(new Type[0]).Invoke(new object[0] );
								}
                                this.scriptManager.FileScripts.Add((IFileScript)script);
							}
                            if (type.GetInterface("IFtpFolderScript") != null)
                            {
								if ( script == null )
								{
									script = (IScript)type.GetConstructor(new Type[0]).Invoke(new object[0] );
								}
                                this.scriptManager.FtpFolderScripts.Add((IFtpFolderScript)script);

                            }
                            if (type.GetInterface("IFTPFileScript") != null)
                            {
                                if (script == null)
                                {
                                    script = (IScript)type.GetConstructor(new Type[0]).Invoke(new object[0]);
                                }
                                this.scriptManager.FTPFileScripts.Add((IFTPFileScript)script);

                            }
						}
					}
				}



				KExplorerControl.singleScriptManager = this.scriptManager;

				this.scriptManager.ScriptHelper = new ScriptHelper( this.scriptManager
					, this, this.form, this );



				this.scriptManager.InitializeScripts( this.scriptManager.ScriptHelper );
				
			} 
			else 
			{
				this.scriptManager = KExplorerControl.singleScriptManager;
			}

			this.InitializeContextMenus( null );


		}


		/// <summary>
		/// Re-build the context menus.
		/// </summary>
		private void InitializeContextMenus( DirectoryInfo dir )
		{

			Menu.MenuItemCollection items = this.form.DirTreeMenu.MenuItems;
			items.Clear();

			List<MenuItem> menuList = new List<MenuItem>();


            if (this.isFtpSite)
            {
                foreach (IScript script in this.scriptManager.FtpFolderScripts ){
                    MenuItem temp = new MenuItem(script.LongName
                                                , new EventHandler(this.FolderScriptMenuItemHandler)
                                                , script.ScriptShortCut);

                    temp.Enabled = script.Active;
                    menuList.Add(temp);
                }
            }
            else
            {

                this.form.DirTreeMenu.MenuItems.Clear();
                this.form.DirTreeMenu.MenuItems.AddRange(

                    this.scriptManager.FolderScripts.Where(fs => (fs.ValidatorFolder == null || fs.ValidatorFolder(dir)))
                    .Select(fs => new MenuItem(fs.LongName, new EventHandler(this.FolderScriptMenuItemHandler), fs.ScriptShortCut) { Enabled = fs.Active })
                    .OrderBy(m => m.Text)
                    .ToArray()
                    );

            }

 
			this.form.FileGridMenuStrip.Items.Clear();

            this.scriptManager.FileScripts.OrderBy(fs => fs.LongName)
                .ToList()
                .ForEach(fs =>
                    this.form.FileGridMenuStrip.Items.Add(fs.LongName)
                    );

            


            var scripts = new List<IScript>();
            if (isFtpSite)
            {
                scripts.AddRange(this.scriptManager.FTPFileScripts.ToArray());
            }
            else
            {
                scripts.AddRange(this.scriptManager.FileScripts.ToArray());
            }
           


		}


		/// <summary>
		/// Handle running a script.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void FolderScriptMenuItemHandler( object sender, EventArgs args )
		{
			MenuItem item = (MenuItem)sender;

            if (this.isFtpSite)
            {
                this.scriptManager.RunFtpFolderScript( item.Text 
                    ,(KexplorerFtpNode)this.form.TreeView1.SelectedNode );
            }
            else
            {

                this.scriptManager.RunFolderScript(item.Text
                                            , (this.clickedNode == null) ?
                                                    (KExplorerNode)this.form.TreeView1.SelectedNode
                                                : this.clickedNode);
            }
		}
		
		/// <summary>
		/// Handle running a script.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void FileScriptMenuItemHandler( object sender, EventArgs args )
		{
			var item = sender as ToolStripItem;
			DataGridView dgrid = this.form.DataGridView1;


            if (!dgrid.CurrentRow.Selected)
            {
                dgrid.ClearSelection();
                dgrid.CurrentRow.Selected = true;
            }


                if (this.isFtpSite)
                {
                    this.scriptManager.RunFtpFileScript(item.Text
                        , (KexplorerFtpNode)this.form.TreeView1.SelectedNode
                        , dgrid.SelectedRows.Cast<DataGridViewRow>().Select( r => (string)r.Cells["Name"].Value )
                                             .ToArray() );
                }
                else
                {
                    this.scriptManager.RunFileScript(item.Text
                        , (KExplorerNode)this.form.TreeView1.SelectedNode
                        ,dgrid.SelectedRows.Cast<DataGridViewRow>().Select( r => (FileInfo)r.Cells["Name"].Value )
                                             .ToArray() );
                }
			

			


		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// After Expanding a node, make sure two levels deeper are expanded.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeView1_AfterExpand(object sender, TreeViewEventArgs e)
		{

			if ( !this.isGuiBeingChanged )
			{
                if (e.Node is KExplorerNode)
                {
                    KExplorerNode kNode = (KExplorerNode)e.Node;
                    string drive = kNode.DirInfo.FullName.Substring(0, 1);
                    Pipeline drivePipeline = (Pipeline)this.drivePipelines[drive];
                    drivePipeline.AddJob(new FolderWorkUnit(kNode, this.form, this));
                }
                else
                {
                    KexplorerFtpNode ftpNode = (KexplorerFtpNode)e.Node;
                    Pipeline ftppipeline = (Pipeline)this.drivePipelines[ftpNode.Site.host];
                    ftppipeline.AddJob(new FtpFolderWorkUnit(ftpNode, this.form, this));
                }
			}

		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Look for function keys, etc.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeView1_KeyDown(object sender, KeyEventArgs e)
		{
			if ( e.KeyCode == Keys.F10 )
			{
				DirPerfStat.Instance().WriteResults();
			}
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Double Clicking in the treeview gets you a command window
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeView1_DoubleClick(object sender, EventArgs e)
		{
			// Handled by a script now.
			/*this.scriptManager.ScriptHelper.RunProgram( "cmd",""
				,(this.clickedNode==null) ? 
							(KExplorerNode)this.form.TreeView1.SelectedNode 
						: this.clickedNode
				); */

		}


        //-----------------------------------------------------------------------------//
        /// <summary>
        /// When a node is selected.  Start the job to load the datagrid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeView1_AfterFtpSelect(object sender, TreeViewEventArgs e)
        {


            KexplorerFtpNode kNode = (KexplorerFtpNode)e.Node;

            if (kNode.Stale)
            {
                if (kNode.Path == null)
                {

                    
                    Pipeline drivePipeline = (Pipeline)this.drivePipelines[kNode.Site.host];
                    if (drivePipeline == null)
                    {
                        drivePipeline = new Pipeline(this.form);

                        this.drivePipelines[kNode.Site.host] = drivePipeline;

                        drivePipeline.StartWork();
                    }
                    drivePipeline.AddJob(new FtpLoaderWorkUnit(kNode, kNode.Site, this.form, this));
                }
                else
                {


                    Pipeline drivePipeline = (Pipeline)this.drivePipelines[kNode.Site.host];
                    if (drivePipeline == null)
                    {
                        drivePipeline = new Pipeline(this.form);

                        this.drivePipelines[kNode.Site.host] = drivePipeline;

                        drivePipeline.StartWork();
                    }
                    drivePipeline.AddJob(new FtpFolderWorkUnit(kNode, this.form, this));
                }

                //this.pipeline.AddJob( new FolderWorkUnit( kNode, this.form, this ) );
            }

            // File List always gets done in the main pipeline.
            this.pipeline.AddPriorityJob(new FtpFileListWorkUnit(kNode, this.form, this));


        }

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// When a node is selected.  Start the job to load the datagrid.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{


			KExplorerNode kNode = (KExplorerNode)e.Node;

			if ( kNode.Stale )
			{
				if ( kNode.DirInfo == null )
				{
					
					string emptyDrive = kNode.Text.Substring(0,1);
					Pipeline drivePipeline = (Pipeline)this.drivePipelines[ emptyDrive ];
					if ( drivePipeline == null )
					{
						drivePipeline = new Pipeline( this.form );

						this.drivePipelines[ emptyDrive ] = drivePipeline;

						drivePipeline.StartWork();
					}
					drivePipeline.AddJob( new DriveLoaderWorkUnit( kNode, emptyDrive, this.form, this ));
				} 
				else 
				{
					// Refresh the node if it's stale...
					string drive = kNode.DirInfo.FullName.Substring(0,1);
					
					Pipeline drivePipeline = (Pipeline)this.drivePipelines[ drive ];
					if ( drivePipeline == null )
					{
						drivePipeline = new Pipeline( this.form );

						this.drivePipelines[ drive ] = drivePipeline;

						drivePipeline.StartWork();
					}	
					drivePipeline.AddJob( new FolderWorkUnit( kNode , this.form, this ));
				}

				//this.pipeline.AddJob( new FolderWorkUnit( kNode, this.form, this ) );
			}

			// File List always gets done in the main pipeline.
			this.pipeline.AddPriorityJob( new FileListWorkUnit( kNode, this.form, this ));

			
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// See if we'll launch the file.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DataGrid1_KeyDown(object sender, KeyEventArgs e)
		{

			if ( e.KeyCode == Keys.Enter )
			{
				DataGrid dgrid = (DataGrid)sender;

				DataView dView = (DataView)dgrid.DataSource;
				

				CurrencyManager bm = (CurrencyManager)dgrid.BindingContext[ dView ];


			
				DataRow currRow = ((DataRowView)bm.Current).Row;

				//DataRow currRow = dView.Table.Rows[ dgrid.CurrentRowIndex ];

				

				if ( currRow != null )
				{
					FileInfo fileInfo = (FileInfo)currRow["Name"];
					this.launcher.Launch( fileInfo );
				}
			}
				
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Double click on the datagrid, is same as launching...
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DataGrid1_DoubleClick(object sender, EventArgs e)
		{
			DataGridView dgrid = (DataGridView)sender;

			DataView dView = (DataView)dgrid.DataSource;


			BindingManagerBase bm = dgrid.BindingContext[ dView ];
			DataRow currRow = ((DataRowView)bm.Current).Row;
		

			//DataRow currRow = dView.Table.Rows[ dgrid.CurrentRowIndex ];

			if ( currRow != null )
			{
				FileInfo fileInfo = (FileInfo)currRow["Name"];
				this.launcher.Launch( fileInfo );
			}
		}




		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Flags the most recently clicked node.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeView1_MouseDown(object sender, MouseEventArgs e)
		{
		
			this.clickedNode  = (KExplorerNode)this.form.TreeView1.GetNodeAt(e.X,e.Y);
		}

		/// <summary>
		/// Just before a menu pops up, re-initialize it every time.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ContextMenu_Popup(object sender, EventArgs e)
		{
			this.InitializeContextMenus((this.isFtpSite)? null : this.clickedNode.DirInfo);
		}

        void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            

            var menu = sender as ContextMenuStrip;
			menu.Items.Clear();

			DataGridView dgrid = this.form.DataGridView1;


            

            FileInfo fileInfo = null;
            if (dgrid.CurrentRow != null)
            {
                fileInfo = dgrid.CurrentRow.Cells["Name"].Value as FileInfo;
            }
            else
            {
                return;
            }

				ArrayList x = new ArrayList();

                scriptManager.FileScripts
                    .Where(fs => (fs.Validator == null || fs.Validator(fileInfo))
                               && (fs.ValidExtensions == null || fs.ValidExtensions.Contains(fileInfo.Extension.ToLower())))
                    .OrderBy( fs => fs.LongName )
                    .ToList()
                    .ForEach(fss => menu.Items.Add(fss.LongName,null, new EventHandler( this.FileScriptMenuItemHandler )));
   

		}


		#region IWorkGUIFlagger Members

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Sets a flag.
		/// </summary>
		public void SignalBeginGUI()
		{
			this.isGuiBeingChanged = true;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Clears a flag.
		/// </summary>
		public void SignalEndGUI()
		{
			this.isGuiBeingChanged = false;
		}


		#endregion

		#endregion


		#region Inner classes.
		public class RowComparer : IComparer
		{
			string[] sortString = null;
			string fldName = null;
			public RowComparer( string newSortString )
			{
				this.sortString = newSortString.Split(' ');


				// The field name is enclosed in brackets...
                this.fldName = this.sortString[0];
                if (this.fldName.StartsWith("["))
                {
                    this.fldName = this.sortString[0].Substring(1, this.sortString[0].Length - 2);
                }


			}
			public int Compare(object x, object y)
			{
				try 
				{
					string xfileName = null;
					string yfileName = null;

					DataRowView xx = (DataRowView)x;
					DataRowView yy = (DataRowView)y;



				
					IComparable xname = null;

					IComparable yname = null;


					try 
					{
						xname = (IComparable)xx[this.fldName];

	
					} 
					catch (Exception e)
					{
						Console.WriteLine("Problem with apples to oranges??.."+ e.Message);
						throw e;
					}
					try 
					{
					

						yname = (IComparable)yy[this.fldName];
					} 
					catch (Exception e)
					{
						Console.WriteLine("Problem with apples to oranges??.."+ e.Message);
						throw e;
					}
					xfileName = xx["Name"].ToString();
					yfileName = yy["Name"].ToString();


					if ( sortString.Length > 1 && sortString[1].ToLower().StartsWith("d") )
					{
						IComparable tvar = xname;
						xname = yname;
						yname = tvar;

						tvar = xfileName;
						xfileName = yfileName;
						yfileName = (string)tvar;
					}


					int result = xname.CompareTo( yname );

					if ( result == 0 )
					{
						result = xfileName.CompareTo( yfileName );
					}


					return result;
				} 
				catch (Exception e)
				{
					Console.WriteLine("Problem...");
					throw e;
				}
			}

		}

		#endregion

		public int Compare(object x, object y)
		{
			return x.ToString().CompareTo(y.ToString());
		}
	}



}
