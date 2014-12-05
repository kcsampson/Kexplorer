using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Linq;
using Kexplorer.scripting;
using System.Text.RegularExpressions;

namespace Kexplorer.services
{


	/// <summary>
	/// Get system services, put them in the gui.
	/// 
	/// This Manage also works like a controller for coordinating scripts.  Etc.  And, it has a pipeline
	/// </summary>
	public class ServiceMgrWorkUnit : IWorkUnit, IKExplorerControl, IWorkGUIFlagger 
	{

		private DataTable table = null;
		private DataView view = null;

		private bool stop = false;

		private IServiceGUI serviceGUI = null;

		private ISimpleKexplorerGUI mainform = null;

		private bool isGuiChanging = false;

		private Pipeline pipeline = null;


		private IScriptMgr scriptManager = null;

		private static IScriptMgr singleScriptManager = null;



        // Now follows format "displayname//machinename"
        private List<string> visibleServicesList = null;


        private string MachineName { get; set; }

        private string SearchPattern { get; set; }

        
		

		public ServiceMgrWorkUnit( ISimpleKexplorerGUI newMainForm, IServiceGUI newServiceGUI)
		{
			
			this.mainform = newMainForm;
			this.serviceGUI = newServiceGUI;

		}

		#region Initialization ----------------------------------------------------------
		public void InitalizeControl(ArrayList visibleServices, string machineName, string searchPattern )
		{

			
			if ( visibleServices != null )
			{
                this.visibleServicesList = visibleServices.Cast<string>().ToList();

			}

            this.MachineName = machineName;
            this.SearchPattern = searchPattern;
       
            
			this.pipeline = new Pipeline( (ISimpleKexplorerGUI) this.serviceGUI );

			this.pipeline.AddJob( this );

			this.pipeline.StartWork();

			this.InitalizeScriptManager();

		}

		#endregion ----------------------------------------------------------------------


		#region Scripting ---------------------------------------------------------------

		private void InitalizeScriptManager()
		{
			if ( ServiceMgrWorkUnit.singleScriptManager == null )
			{
                this.scriptManager = new ScriptMgr();
                ServiceMgrWorkUnit.singleScriptManager = this.scriptManager;


				ArrayList serviceScripts = new ArrayList();


				Assembly x = this.GetType().Assembly;
				foreach ( Module module in x.GetModules() )
				{
				
					foreach ( Type type in module.GetTypes() )
					{
						IScript script = null;
						if ( type.IsClass && !type.IsAbstract )
						{
							if ( type.GetInterface("IServiceScript" ) != null )
							{
							
								script = (IScript)type.GetConstructor(new Type[0]).Invoke(new object[0] );
								this.scriptManager.ServiceScripts.Add( (IServiceScript) script );
							}

					
						}
					}
				}


				this.scriptManager.ScriptHelper = new ScriptHelper( this.scriptManager
					, this, this.mainform, this );

	
				this.scriptManager.InitializeScripts( this.scriptManager.ScriptHelper );
				
			} 
			else 
			{
				this.scriptManager = ServiceMgrWorkUnit.singleScriptManager;
			}

			this.InitializeContextMenus();

			this.serviceGUI.ServiceGrid.ContextMenu.Popup += new EventHandler(ContextMenu_Popup);


		}


		/// <summary>
		/// Re-build the context menus.
		/// </summary>
		private void InitializeContextMenus()
		{

			Menu.MenuItemCollection items = this.serviceGUI.ServiceGridMenu.MenuItems;
			items.Clear();

			foreach ( IScript script in this.scriptManager.ServiceScripts )
			{
				MenuItem temp = new MenuItem( script.LongName
					, new EventHandler( this.HandleServiceGridMenu )
					, script.ScriptShortCut );

				temp.Enabled = script.Active;

				items.Add( temp );

			}	


		}




		#endregion ----------------------------------------------------------------------


		#region Menu Events

		private void HandleServiceGridMenu( object sender, EventArgs e )
		{
			MenuItem item = (MenuItem)sender;

			this.scriptManager.RunServiceScript( item.Text
				,this.GetSelectedServices()
				,this.mainform.MainForm
				,this.serviceGUI.ServiceGrid);
			
		}

		private void ContextMenu_Popup(object sender, EventArgs e)
		{
			Menu.MenuItemCollection items = this.serviceGUI.ServiceGridMenu.MenuItems;
			items.Clear();

			DataGrid dgrid = this.serviceGUI.ServiceGrid;

			DataView dView = (DataView)dgrid.DataSource;

			ServiceController[] selectedServices = null;

			selectedServices = this.GetSelectedServices();


			foreach ( IServiceScript script in this.scriptManager.ServiceScripts )
			{
				if ( script.Validator != null )
				{
					bool hit = false;
					foreach ( ServiceController service in selectedServices )
					{
						if ( !script.Validator( service ) )
						{
							hit = true;
						}
					}
					if ( hit )
					{
						continue;
					}
				}


				
				MenuItem temp = new MenuItem( script.LongName
					, new EventHandler( this.HandleServiceGridMenu )
					, script.ScriptShortCut );

				temp.Enabled = script.Active;

				items.Add( temp );


			}	

		}



		/// <summary>
		/// Get the selected rows from the DataGrid...
		/// </summary>
		/// <returns></returns>
		private ServiceController[] GetSelectedServices()
		{
			ServiceController[] result = null;

			DataGrid dgrid = this.serviceGUI.ServiceGrid;

			DataView dView = (DataView)dgrid.DataSource;
			
			CurrencyManager bm = (CurrencyManager)dgrid.BindingContext[ dView ];
			ArrayList arrSelectedRows = new ArrayList();
			
			
			dView = (DataView) bm.List;	

			ArrayList sortedItems = new ArrayList();
			sortedItems.AddRange( dView.Table.Rows );

			DataRow[] rows = (DataRow[])sortedItems.ToArray( typeof( DataRow ));

			if ( dView.Sort != null && dView.Sort.Length > 0 )
			{
				try 
				{
					Array.Sort( rows, new ServiceMgrWorkUnit.RowComparer( dView.Sort ));
				} 
				catch (Exception e)
				{
					Console.WriteLine("Unable to sort the rows as they were in the grid:" + e.Message );
				}
			}
	
			

			// See if multi's are selected
			ArrayList selectedFiles = new ArrayList();
			for ( int i = 0; i < dView.Table.Rows.Count; i++ )
			{
				
				if ( dgrid.IsSelected(i))
				{
					selectedFiles.Add(rows[i]["ServiceControllerObject"]);
				}
			
			}
	

			result = (ServiceController[])selectedFiles.ToArray( typeof( ServiceController ) );

			
			return result;
		}
		#endregion

		#region IWorkUnit ---------------------------------------------------------------

		public IWorkUnit DoJob()
		{


			this.table = new DataTable("Services");
			DataColumn c = null;
			c = new DataColumn();
											 
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "Name";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "Status";
			c.ReadOnly = false;
			c.Unique = false;
			table.Columns.Add(c);	


			c = new DataColumn();
			c.DataType = System.Type.GetType("System.Boolean");
			c.ColumnName = "CanPauseAndContinue";
			c.ReadOnly = false;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.Boolean");
			c.ColumnName = "CanShutdown";
			c.ReadOnly = false;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			
			c.DataType = System.Type.GetType("System.Boolean");
			c.ColumnName = "CanStop";
			c.ReadOnly = false;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "SystemName";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);


			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "ServiceType";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);


			c = new DataColumn();
			c.DataType = System.Type.GetType("System.Object");
			c.ColumnName = "ServiceControllerObject";
			c.ReadOnly = true;
			c.Unique = false;
		
			table.Columns.Add(c);

            c = new DataColumn();
            c.DataType = System.Type.GetType("System.String");
            c.ColumnName = "Machine";
            c.ReadOnly = true;
            c.Unique = false;

            table.Columns.Add(c);
     
            var services = new List<ServiceController>();
            if (this.visibleServicesList == null)
            {
                if ( string.IsNullOrEmpty( this.MachineName)) {
                    this.MachineName = ".";
                }
                services = System.ServiceProcess.ServiceController
                    .GetServices(this.MachineName)
                    .Where(sc => string.IsNullOrEmpty(this.SearchPattern)
                                    || Regex.Match(sc.DisplayName, this.SearchPattern).Length > 0)

                    .ToList().OrderBy(s => s.ServiceName)
                    .ToList();

            }
            else
            {
                var visibles = this.visibleServicesList.ToList()
                                                       .Select( s => new { DisplayName = (string)((s.Contains("//")) ? s.Substring(0,s.IndexOf("//") ) : s),
                                                                           MachName = (string)((s.Contains("//")) ? s.Substring(s.IndexOf("//") + 2) : ".")})
                                                        .Distinct()
                                                        .ToList();

                var visibleDict = visibles.ToDictionary( a => a, a => a );

                // Get all the machine names.....
                visibles.Select(s => s.MachName )
                        .Distinct()
                        .ToList()
                        .ForEach( m => services.AddRange(
                            ServiceController.GetServices(m)
                                             .Where( sc => visibleDict.ContainsKey(new {DisplayName = sc.DisplayName, MachName = sc.MachineName } ) )
                                             ) 
                            );

                services = (from v in visibles
                            join s in services on v equals new { s.DisplayName, MachName= s.MachineName }
                            select s).ToList();

            }




			foreach ( var service in services )
			{

				if ( this.stop )
				{
					return null;
				}
 
				DataRow row = this.table.NewRow();                               

				row["Name"] = service.DisplayName;

				row["Status"] = service.Status.ToString();


				row["SystemName"] = service.ServiceName;

				row["CanPauseAndContinue"] = service.CanPauseAndContinue;

				row["CanShutdown"] = service.CanShutdown;

				row["CanStop"] = service.CanStop;
				row["ServiceType"] = service.ServiceType.ToString();

				row["ServiceControllerObject"]  = service;

                row["Machine"] = service.MachineName;

				table.Rows.Add( row );

			}

			// Insert code to create and populate columns.
			this.view = new DataView(table);

			
			if ( !this.stop )
			{
				this.mainform.MainForm.Invoke( new InvokeDelegate( this.SetDataToDataGrid));
			}

			return null;
		}


		private void SetDataToDataGrid()
		{
			this.serviceGUI.ServiceGrid.CaptionText = "Services";
			this.serviceGUI.ServiceGrid.DataSource = this.view;

			try 
			{
				DataGridTableStyle ts = null; 
				if ( this.serviceGUI.ServiceGrid.TableStyles["Services"] != null )
				{
					ts = this.serviceGUI.ServiceGrid.TableStyles["Services"];
				} 
				else 
				{
					ts = new DataGridTableStyle();
					ts.MappingName = "Services";
					this.serviceGUI.ServiceGrid.TableStyles.Clear();
					this.serviceGUI.ServiceGrid.TableStyles.Add(ts);
					this.serviceGUI.ServiceGrid.TableStyles["Services"].GridColumnStyles["Name"].Width = 250;

					
					this.serviceGUI.ServiceGrid.TableStyles["Services"
						].GridColumnStyles["ServiceControllerObject"].Width = 0;

				}
				
																

			} 
			catch (Exception e )
			{
				Console.WriteLine("Error setting Name column width."  + e.Message );
			}
		}


		#endregion ----------------------------------------------------------------------

		#region Various interface default implementation --------------------------------

		public void Close()
		{
			if ( this.pipeline != null )
			{
				this.pipeline.StopWork();
			}
		}
		
		public void Abort()
		{
			this.stop = true;;
		}

		public void YouWereAborted()
		{
			// nothing to do...
		}

		public Pipeline MainPipeLine
		{
			get { return this.pipeline; }
		}

		public Hashtable DrivePipelines
		{
			get { throw new NotImplementedException(); }
		}

		public void SignalBeginGUI()
		{
			this.isGuiChanging = true;
		}

		public void SignalEndGUI()
		{
			this.isGuiChanging = false;
		}

		#endregion ----------------------------------------------------------------------



		#region RowComparer.....
		public class RowComparer : IComparer
		{
			string[] sortString = null;
			string fldName = null;
			public RowComparer( string newSortString )
			{
				this.sortString = newSortString.Split(' ');


				// The field name is enclosed in brackets...
				this.fldName = this.sortString[0].Substring(1, this.sortString[0].Length-2);

			}
			public int Compare(object x, object y)
			{
				try 
				{
					string xfileName = null;
					string yfileName = null;

					DataRow xx = (DataRow)x;
					DataRow yy = (DataRow)y;
				
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
	}

	#endregion
}
