using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Kexplorer.console;
using Kexplorer.res;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Summary description for ScriptHelper.
	/// </summary>
	public class ScriptHelper : IScriptHelper
	{
		private IScriptMgr scriptMgr = null;
		private IKExplorerControl mainController = null;
		private ISimpleKexplorerGUI mainGUI = null;
		private IWorkGUIFlagger mainGUIFlagger = null;

		private XmlDocument scriptHelperDoc = null;


		public void InitScriptHelperDoc()
		{
			
			this.scriptHelperDoc = new XmlDocument();

			if ( File.Exists( "scripthelper.xml"))
			{
				this.scriptHelperDoc.Load( "scripthelper.xml");
			} else
			{
				this.scriptHelperDoc.LoadXml(

							"<ScriptHelper>"
							+"\n<!--    exe names will be tolowered before searching. -->"
							+"\n<ProgramLocations>"
							+"\n<ProgramAlias exe='textpad.exe'>c:\\kimball\\textpadxxx\\Textpad\\TextPad.exe</ProgramAlias>"
							+"\n</ProgramLocations>"
							+"\n</ScriptHelper>"					
					);
				this.scriptHelperDoc.Save("scripthelper.xml");
			}
		}

		private KExplorerNode renamedNode = null;

        private KexplorerFtpNode renamedFtpNode = null;

		public ScriptHelper( IScriptMgr mgr
			, IKExplorerControl  controller 
			, ISimpleKexplorerGUI newMainGUI
			, IWorkGUIFlagger newMainGUIFlagger )
		{
			this.scriptMgr = mgr;
			this.mainController = controller;
			this.mainGUI = newMainGUI;
			this.mainGUIFlagger = newMainGUIFlagger;


			if ( scriptHelperDoc == null )
			{
				this.InitScriptHelperDoc();
				
			}
		}

		public KExplorerNode FindFolder(KExplorerNode startNode, string relativePath)
		{
			throw new NotImplementedException();
		}

		private Hashtable vars = new Hashtable();
		public Hashtable VARS
		{
			get { return this.vars; }
		}

		public IScript FindScript(string scriptName)
		{
            List<IScript> allScripts = new List<IScript>();
            allScripts.AddRange(scriptMgr.FileScripts.ToArray());
            allScripts.AddRange(scriptMgr.FolderScripts.ToArray());
            allScripts.AddRange(scriptMgr.FTPFileScripts.ToArray());
            allScripts.AddRange(scriptMgr.FtpFolderScripts.ToArray());
            allScripts.AddRange(scriptMgr.ServiceScripts.ToArray());

            try
            {
              

                return allScripts.Where(script => script.LongName.Equals(scriptName)
                        || script.GetType().Name.Equals(scriptName)).First();
            }
            catch (Exception e)
            {
                return null;
            }


		}

		public void AddScript(IScript script)
		{

			throw new NotImplementedException();
		}

		public void RemoveScript(string scriptName)
		{
			throw new NotImplementedException();
		}

        public void RefreshFolder(KexplorerFtpNode folderNode, bool folderOnly)
        {
            /*if (folderNode.Parent != null
                && folderNode.Text != folderNode.Path)
            {

                this.renamedFtpNode = folderNode;
                this.mainGUI.MainForm.Invoke(new InvokeDelegate(this.RefreshNodeName));
            } */
            if (!folderOnly)
            {
                this.mainController.MainPipeLine.AddPriorityJob(new FtpFileListWorkUnit(folderNode, this.mainGUI
                    , this.mainGUIFlagger));
            }

        }

		public void RefreshFolder(KExplorerNode folderNode, bool folderOnly )
		{

			folderNode.Stale = true;

			string drive = null;
			// Make sure it's initialized...
			if ( folderNode.DirInfo == null )
			{
				drive = folderNode.Text.Substring(0,1);
			} 
			else if ( !folderNode.Text.Equals( folderNode.DirInfo.Name ) )
			{
				// Make sure it isn't a drive letter node.
				if ( !folderNode.Text.Substring(1,1).Equals(":") )
				{
					this.renamedNode = folderNode;

					this.mainGUI.MainForm.Invoke( new InvokeDelegate( this.RefreshNodeName ) );

				}
				drive = folderNode.DirInfo.FullName.Substring(0,1);
			} 
			else 
			{
				drive = folderNode.DirInfo.FullName.Substring(0,1);
			}



			Pipeline drivePipeline = (Pipeline)this.mainController.DrivePipelines[ drive ];
			if ( drivePipeline == null )
			{
				// The maincontroller is initialized from only one Tab and if that tab
				// removed from view a certain drive, then there'll be no drive pipeline
				// for it.  So, just make it here.
				drivePipeline = new Pipeline( this.mainGUI );
				this.mainController.DrivePipelines[ drive ] = drivePipeline;

				drivePipeline.StartWork();
			}

			// If it's a drive, perhaps now it's online...
			if ( folderNode.Text.Substring(1,1).Equals(":" ))
			{
				drivePipeline.AddJob( new DriveLoaderWorkUnit( folderNode, drive, this.mainGUI, this.mainGUIFlagger ));

				return;

			} 
			drivePipeline.AddJob( new FolderWorkUnit( folderNode
				, this.mainGUI
				, this.mainGUIFlagger ));

			//this.mainController.MainPipeLine.AddJob( new FolderWorkUnit( folderNode
			//													, this.mainGUI
			//													, this.mainGUIFlagger ) );

			if ( !folderOnly )
			{
				this.mainController.MainPipeLine.AddPriorityJob( new FileListWorkUnit( folderNode, this.mainGUI
					, this.mainGUIFlagger ));
			}
		}


		private void RefreshNodeName()
		{

			lock( this.mainGUI.TreeView1 )
			{
				this.renamedNode.Text = this.renamedNode.DirInfo.Name;
			}
		}

        private void RefreshFtpNodeName()
        {

            lock (this.mainGUI.TreeView1)
            {
                this.renamedFtpNode.Text = this.renamedFtpNode.Path;
            }
        }

		public void RunProgram( string exe, string options, KExplorerNode atFolderNode, bool waitForExit, bool asAdmin )
		{

            string otherExe = exe.Substring(exe.LastIndexOf("\\") + 1);
			//First we'll see of the exe is in the scripthelperxml program locations.
			XmlNode node = this.scriptHelperDoc.SelectSingleNode("//ProgramAlias[@exe='"+otherExe.ToLower()+"']");
			string foundexe;
			if ( node != null )
			{
				
				foundexe = node.InnerText;
			} else
			{



				foundexe = exe;
				
			}
			
			Process cmd = new Process();

			cmd.StartInfo.FileName = foundexe;

			

			if ( options != null )
			{
				cmd.StartInfo.Arguments = options;
                if (asAdmin && !IsAdministrator())
                {
                    cmd.StartInfo.Verb = "runas";
                }
			}
			
			KExplorerNode kNode = atFolderNode;

			if ( kNode != null )
			{
				cmd.StartInfo.WorkingDirectory = kNode.DirInfo.FullName;

			}



			cmd.Start();

			if ( waitForExit )
			{
				cmd.WaitForExit();
			}


		}

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            if (null != identity)
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return false;
        }

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Launch a program
		/// </summary>
		/// <param name="exe"></param>
		/// <param name="options"></param>
		public void RunProgram( string exe, string options, KExplorerNode atFolderNode, bool asAdmin )
		{

			this.RunProgram( exe, options, atFolderNode, false, asAdmin );

		}

        public void RunProgram(string exe, string options, KExplorerNode atFolderNode)
        {

            this.RunProgram(exe, options, atFolderNode, false, false);

        }

		public void RunProgramInKonsole( string exe, string options, KExplorerNode atFolderNode )
		{

			//First we'll see of the exe is in the scripthelperxml program locations.
			XmlNode node = this.scriptHelperDoc.SelectSingleNode("//ProgramAlias[@exe='"+exe.ToLower()+"']");
			string foundexe;
			if ( node != null )
			{
				
				foundexe = node.InnerText;
			} 
			else
			{
				foundexe = exe;
	
			}

			KExplorerConsole console = new KExplorerConsole(KMultiForm.Instance());

			TabPage x = new TabPage("Konsole->" );


			console.Dock = System.Windows.Forms.DockStyle.Fill; 

			x.Controls.Add( console );


			KMultiForm.Instance().MainTabControl.TabPages.Add( x );	
			

			KMultiForm.Instance().MainTabControl.SelectedTab = x;


			console.Initialize(atFolderNode.FullPath);


			console.TypeCommand(foundexe + " " + options );
	
		}

        public XmlDocument ScriptHelperDoc
        {
            get
            {
                if (this.scriptHelperDoc == null)
                {
                    this.InitScriptHelperDoc();
                }
                return this.scriptHelperDoc;
            }

            // set to null, so it re-initializes on next Get.
            set { 
                this.scriptHelperDoc = value;
                if (value == null)
                {
                    this.InitScriptHelperDoc();
                }
            }

        }


        public List<string> GetValueList(String configXPath)
        {

            List<String> values = new List<string>();

            XmlNodeList nodes = this.scriptHelperDoc.SelectNodes(configXPath);
            if (nodes != null)
            {

                foreach (XmlNode node in nodes)
                {

                    if (node is XmlAttribute)
                    {

                        values.Add(node.Value);
                    }
                    else
                    {
                        values.Add(node.InnerText);
                    }
                }
            }

            return values;
        }

    }




}
