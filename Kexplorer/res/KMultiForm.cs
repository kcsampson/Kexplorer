using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Kexplorer.console;
using Kexplorer.services;
using Kexplorer.scripting;
using System.Collections.Generic;
using System.Threading;

namespace Kexplorer.res
{
	/// <summary>
	/// Summary description for KMultiForm.
	/// </summary>
	public class KMultiForm : System.Windows.Forms.Form
	{
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.ContextMenu tabMenu;
		private System.Windows.Forms.MenuItem menuNewTab;
		private System.Windows.Forms.MenuItem menuCloseTab;
		private System.Windows.Forms.MenuItem menuSaveView;
		private System.Windows.Forms.MenuItem menuItemServices;
		private System.Windows.Forms.MenuItem menuItem2;
		private System.Windows.Forms.MenuItem menuItemConsole;

		

		public static KMultiForm instance = null;
        private MenuItem menuItemFtp;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
               Application.ThreadException += new ThreadExceptionEventHandler( OnError);
       
            AppDomain.CurrentDomain.UnhandledException +=new UnhandledExceptionEventHandler(OnUnhandled);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                Application.Run(new KMultiForm());

		}


        static void OnError(object sender, ThreadExceptionEventArgs te)
        {
            File.AppendAllText("kexploere.log", "\n" + te.Exception.StackTrace + "\n");
           

        }

        static void OnUnhandled(object sender, UnhandledExceptionEventArgs ue)
        {

            File.AppendAllText("kexplorer.log", "Unhandled Exception\n" + ue.ExceptionObject.ToString() + "\n");
        }

	

		public KMultiForm()
		{
			KMultiForm.instance = this;
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();


			this.LoadSavedPanels();


			this.tabControl1.DoubleClick += new EventHandler(tabControl1_DoubleClick);


			this.tabControl1.SelectedIndexChanged += new EventHandler(tabControl1_SelectedIndexChanged);


		}


		/// <summary>
		/// Load state, or, start with a fresh new panel.
		/// </summary>
		private void LoadSavedPanels()
		{
			string saveFile = Application.StartupPath + "\\KexplorerStateSave.xml";
			if (!File.Exists( saveFile ) )
			{
				this.AddNewPanel();
			} else {


				XmlDocument savedDoc = new XmlDocument();
				savedDoc.Load( saveFile );


				XmlNodeList panels = savedDoc.SelectNodes("/KexplorerState/KexplorerTab");

				if ( panels.Count == 0 )
				{
					this.AddNewPanel();

				} 
				else 
				{

					TabPage selectedPage = null;
					foreach ( XmlNode panel in panels )
					{

						XmlNode nameNode = panel.SelectSingleNode("TabName");

						XmlNode servicesNode = panel.SelectSingleNode("ServicesTab");

                        XmlNode ftpSiteNode = panel.SelectSingleNode("FtpTab");

						if ( servicesNode != null )
						{

							XmlNodeList visibleServices = panel.SelectNodes("Service");
							ArrayList serviceNames = new ArrayList();
							foreach ( XmlNode serviceNode in visibleServices )
							{
								serviceNames.Add( serviceNode.InnerText );
							}


							TabPage x = new TabPage( nameNode.InnerText);


							if ( panel.SelectSingleNode( "Selected" ) != null )
							{
								selectedPage = x;

							}
							ServicesPanel servicesPanel = new ServicesPanel( this );

							ServiceMgrWorkUnit worker = new ServiceMgrWorkUnit( servicesPanel, servicesPanel);
							servicesPanel.Manager = worker;

							servicesPanel.Dock = System.Windows.Forms.DockStyle.Fill;

							this.tabControl1.TabPages.Add( x );	

							x.Controls.Add( servicesPanel);
							worker.InitalizeControl( serviceNames,null,null );


                        }
                        else if (ftpSiteNode != null)
                        {
                            //
                            // TODO: Add any constructor code after InitializeComponent call
                            //
                            var hostNode = ftpSiteNode.SelectSingleNode("Host");
                            var userNameNode = ftpSiteNode.SelectSingleNode("UserName");
                            var pwdNode = ftpSiteNode.SelectSingleNode("Pwd");
                            var targetFolderNode = ftpSiteNode.SelectSingleNode("TargetFolder");
                            var typeNode = ftpSiteNode.SelectSingleNode("Type");

                            KexplorerPanel newPanel = new KexplorerPanel(this
                                , new FtpSite(
                                    hostNode.InnerText
                                    , userNameNode.InnerText
                                    , pwdNode.InnerText
                                    , targetFolderNode.InnerText
                                    , typeNode.InnerText

                                    ));


                            newPanel.MainForm = this;


                            TabPage y = new TabPage(nameNode.InnerText);


                            newPanel.Dock = System.Windows.Forms.DockStyle.Fill;

                            y.Controls.Add(newPanel);


                            this.tabControl1.TabPages.Add(y);
                        }
                        else
                        {

                            XmlNode currentFolderNode = panel.SelectSingleNode("CurrentFolder");

                            XmlNodeList driveNodes = panel.SelectNodes("Drive");
                            ArrayList drives = new ArrayList();
                            foreach (XmlNode node in driveNodes)
                            {
                                drives.Add(node.InnerText);
                            }

                            KexplorerPanel newPanel = new KexplorerPanel(this, currentFolderNode.InnerText, drives);


                            newPanel.MainForm = this;


                            TabPage x = new TabPage(nameNode.InnerText);


                            newPanel.Dock = System.Windows.Forms.DockStyle.Fill;

                            x.Controls.Add(newPanel);


                            this.tabControl1.TabPages.Add(x);

                            if (panel.SelectSingleNode("Selected") != null)
                            {
                                selectedPage = x;
                                newPanel.InitializeOnce();
                            }
                        }

					}

					if ( selectedPage != null )
					{
						this.tabControl1.SelectedTab = selectedPage;

						
					}
				}




			}
		}

		private void AddNewPanel()
		{
			//
			// TODO: Add any constructor code after InitializeComponent call
			//
			KexplorerPanel newPanel =  new KexplorerPanel( this );


			newPanel.MainForm = this;

	


			TabPage x = new TabPage(this.tabControl1.TabPages.Count.ToString());


			newPanel.Dock = System.Windows.Forms.DockStyle.Fill;

			x.Controls.Add( newPanel);


			this.tabControl1.TabPages.Add( x );




		}


		private void removeCurrentPanel()
		{
			TabPage x = this.tabControl1.SelectedTab;

			if ( x != null )
			{

				this.tabControl1.TabPages.Remove( x );

			}

		

			x.Dispose();
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
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabMenu = new System.Windows.Forms.ContextMenu();
            this.menuNewTab = new System.Windows.Forms.MenuItem();
            this.menuCloseTab = new System.Windows.Forms.MenuItem();
            this.menuSaveView = new System.Windows.Forms.MenuItem();
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.menuItemServices = new System.Windows.Forms.MenuItem();
            this.menuItemConsole = new System.Windows.Forms.MenuItem();
            this.menuItemFtp = new System.Windows.Forms.MenuItem();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.ContextMenu = this.tabMenu;
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(968, 590);
            this.tabControl1.TabIndex = 0;
            // 
            // tabMenu
            // 
            this.tabMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuNewTab,
            this.menuCloseTab,
            this.menuSaveView,
            this.menuItem2,
            this.menuItemServices,
            this.menuItemConsole,
            this.menuItemFtp});
            this.tabMenu.Popup += new System.EventHandler(this.tabMenu_Popup);
            // 
            // menuNewTab
            // 
            this.menuNewTab.Index = 0;
            this.menuNewTab.Text = "New Tab";
            this.menuNewTab.Click += new System.EventHandler(this.menuItem1_Click);
            // 
            // menuCloseTab
            // 
            this.menuCloseTab.Index = 1;
            this.menuCloseTab.Text = "Close Tab";
            this.menuCloseTab.Click += new System.EventHandler(this.menuCloseTab_Click);
            // 
            // menuSaveView
            // 
            this.menuSaveView.Index = 2;
            this.menuSaveView.Text = "Save View";
            this.menuSaveView.Click += new System.EventHandler(this.menuSaveView_Click);
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 3;
            this.menuItem2.Text = "-";
            // 
            // menuItemServices
            // 
            this.menuItemServices.Index = 4;
            this.menuItemServices.Text = "Services";
            this.menuItemServices.Click += new System.EventHandler(this.menuItemServices_Click);
            // 
            // menuItemConsole
            // 
            this.menuItemConsole.Index = 5;
            this.menuItemConsole.Text = "Konsole";
            this.menuItemConsole.Click += new System.EventHandler(this.menuItemConsole_Click);
            // 
            // menuItemFtp
            // 
            this.menuItemFtp.Index = 6;
            this.menuItemFtp.Text = "ftp";
            this.menuItemFtp.Click += new System.EventHandler(this.menuItemFtp_Click);
            // 
            // KMultiForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(968, 590);
            this.Controls.Add(this.tabControl1);
            this.Name = "KMultiForm";
            this.Text = "Kexplorer";
            this.ResumeLayout(false);

		}
		#endregion

		private void menuItem1_Click(object sender, System.EventArgs e)
		{

			this.AddNewPanel();
		
		}

		private void menuCloseTab_Click(object sender, System.EventArgs e)
		{

			this.removeCurrentPanel();
		
		}

		private void tabControl1_DoubleClick(object sender, EventArgs e)
		{
			TabPage x = this.tabControl1.SelectedTab;

			if ( x == null )
			{
				return;
			
			}
			
			string tabNameString = QuickDialog.DoQuickDialog( "New Explorer Tab Name", "Tab Name", x.Text );

			if ( tabNameString !=  null )
			{
				x.Text = tabNameString;
			}


		}

		private void menuSaveView_Click(object sender, System.EventArgs e)
		{
			this.SaveView();
		}



		/// <summary>
		/// Write the current view DefaultView.xml
		/// </summary>
		private void SaveView()
		{


			ArrayList tabPersists = new ArrayList();

			foreach ( TabPage page in this.tabControl1.TabPages )
			{

				ISimpleKexplorerGUI panel = null;
				foreach ( Control control in page.Controls )
				{
					if ( control is KexplorerPanel || control is ServicesPanel)
					{
						panel = (ISimpleKexplorerGUI)control;
						break;
					}
				}
				if ( panel != null )
				{
					bool isSelected = this.tabControl1.SelectedTab == page;
					tabPersists.Add( new TreeViewPersistState( panel, page.Text, isSelected ) );
				}

			}

			XmlDocument stateDoc = new XmlDocument();

			StringBuilder output = new StringBuilder();

			output.Append("<KexplorerState>\n");

			foreach ( TreeViewPersistState tabState  in tabPersists )
			{
				tabState.WriteXmlOutput( output );
			}

			output.Append("</KexplorerState>\n");

			stateDoc.LoadXml( output.ToString());

			stateDoc.Save(Application.StartupPath + "\\KexplorerStateSave.xml");
			
		}

		private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
		{
			TabPage page = this.tabControl1.SelectedTab;

			KexplorerPanel panel = null;
			foreach ( Control control in page.Controls )
			{
				if ( control is KexplorerPanel )
				{
					panel = (KexplorerPanel)control;
					break;
				}
			}
			if ( panel != null )
			{
				panel.InitializeOnce();
			}
		}

		private void menuItemServices_Click(object sender, System.EventArgs e)
		{



            var machineParms = QuickDialog2.DoQuickDialog("Services", "Machine Name", ".", "Pattern ^(Enable|EPX)", "");
            if (machineParms == null)
            {
                machineParms = new string[2];
            }
			//
			// TODO: Add any constructor code after InitializeComponent call
			//
			ServicesPanel newPanel =  new ServicesPanel( this);


			newPanel.MainForm = this;


			TabPage x = new TabPage("Services");


			newPanel.Dock = System.Windows.Forms.DockStyle.Fill;

			x.Controls.Add( newPanel);


			this.tabControl1.TabPages.Add( x );	
		

			ServiceMgrWorkUnit worker = new ServiceMgrWorkUnit( newPanel, newPanel );

			newPanel.Manager = worker;

			worker.InitalizeControl(null,machineParms[0],machineParms[1]);

			this.tabControl1.SelectedTab = x;


		

			

		}

		private void tabMenu_Popup(object sender, System.EventArgs e)
		{
		
		}

		private void menuItemConsole_Click(object sender, System.EventArgs e)
		{

			KExplorerConsole console = new KExplorerConsole(this);

			TabPage x = new TabPage("Konsole");


			console.Dock = System.Windows.Forms.DockStyle.Fill;

			x.Controls.Add( console );


			this.tabControl1.TabPages.Add( x );	
		

			this.tabControl1.SelectedTab = x;


			console.Initialize();

		
		}


        private void menuItemFtp_Click(object sender, System.EventArgs e)
        {

            ScriptHelper helper = new ScriptHelper(null, null, null, null);

            List<String> ftpSites = helper.GetValueList("//ftpsites/ftpsite/@name");

            if (ftpSites == null || ftpSites.Count == 0)
            {


                XmlDocumentFragment xfrag = helper.ScriptHelperDoc.CreateDocumentFragment();
                xfrag.InnerXml ="<ftpsites>\n"
                     + "<ftpsite name=\"Enterworks\" host=\"ewftp.com\" username=\"xxx\" pwd=\"xxx\" folder=\"/Outgoing\" type=\"win\" />\n"
                    + " <ftpsite name=\"Kimbonics\" host=\"kimbonics.com\" username=\"xxxxx\" pwd=\"XXXX\" folder=\"\" type=\"unix\" />\n"
                   + " </ftpsites>";

                helper.ScriptHelperDoc.FirstChild.AppendChild(xfrag);

                helper.ScriptHelperDoc.Save( "scripthelper.xml");

                MessageBox.Show("FTP Site Examples have been added to your scripthelper.xml.  You must edit them with correct information",
                    "Kexplorer" );
               


                return;
            }


            String siteName = (String)QuickSelectListDialog.DoQuickDialog("Kexplorer Easy FTP", "Select a site:", ftpSites.ToArray());

            if (siteName == null)
            {
                return;
            }

            String host = helper.GetValueList("//ftpsites/ftpsite[@name='" + siteName + "']/@host")[0];
            String username = helper.GetValueList("//ftpsites/ftpsite[@name='" + siteName + "']/@username")[0];
            String pwd = helper.GetValueList("//ftpsites/ftpsite[@name='" + siteName + "']/@pwd")[0];

            String targetfolder = helper.GetValueList("//ftpsites/ftpsite[@name='" + siteName + "']/@folder")[0];
            String type = helper.GetValueList("//ftpsites/ftpsite[@name='" + siteName + "']/@type")[0];

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            KexplorerPanel newPanel = new KexplorerPanel(this, new FtpSite(host, username, pwd, targetfolder, type));


            newPanel.MainForm = this;


            TabPage y = new TabPage("ftp:"+siteName);


            newPanel.Dock = System.Windows.Forms.DockStyle.Fill;

            y.Controls.Add(newPanel);


            this.tabControl1.TabPages.Add(y);


        }



		public TabControl MainTabControl 
		{
			get { return this.tabControl1; }
		}

		public static KMultiForm Instance(){ return KMultiForm.instance; }





	}
}
