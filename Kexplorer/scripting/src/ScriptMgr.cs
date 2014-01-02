using System;
using System.IO;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
namespace Kexplorer.scripting
{
	/// <summary>
	/// Holds all the scripts.
	/// </summary>
	public class ScriptMgr : IScriptMgr
	{
		public ScriptMgr()
		{
			

		}
		#region IScriptMgr Members

        private List<IFtpFolderScript> ftpFolderScripts = new List<IFtpFolderScript>();
        public List<IFtpFolderScript> FtpFolderScripts
        {
            get { return this.ftpFolderScripts; }
            set { this.ftpFolderScripts = value; }
        }

        private List<IFTPFileScript> ftpFileScripts = new List<IFTPFileScript>();
        public List<IFTPFileScript> FTPFileScripts
        {
            get { return this.ftpFileScripts; }
            set { this.ftpFileScripts = value; }
        }




		List<IFileScript> fileScripts = new List<IFileScript>();
		public List<IFileScript> FileScripts
		{
			get
			{
			
				return this.fileScripts;
			}
			set
			{
				this.fileScripts = value;
			}
		}

		List<IFolderScript> folderScripts = new List<IFolderScript>();
		public List<IFolderScript> FolderScripts
		{
			get
			{
				return this.folderScripts;
			}
			set
			{
				this.folderScripts = value;
			}
		}

		private List<IServiceScript> serviceScripts = new List<IServiceScript>();
		public List<IServiceScript> ServiceScripts
		{
			get { return this.serviceScripts; }
			set { this.serviceScripts = value; }
		}

		private IScriptHelper scriptHelper = null;
		public IScriptHelper ScriptHelper 
		{
			get { return this.scriptHelper; }
			set { this.scriptHelper = value; }
		}

        public void RunFtpFileScript(string longName, KexplorerFtpNode foldernode, string[] files)
        {
            this.ftpFileScripts.Where(s => s.LongName.Equals(longName)).First().Run(foldernode, files);

        }

        public void RunFtpFolderScript(string longName, KexplorerFtpNode foldernode)
        {
            this.ftpFolderScripts.Where(s => s.LongName.Equals(longName)).First().Run(foldernode);


        }

		public void RunFileScript(string longName, KExplorerNode folderNode, FileInfo[] files)
		{
            this.fileScripts.Where( s => s.LongName.Equals( longName ) ).First().Run( folderNode, files );

		}

		public void RunFolderScript( string longName, KExplorerNode folderNode)
		{
            this.folderScripts.Where( s => s.LongName.Equals( longName ) ).First().Run( folderNode );

		}

		public void RunServiceScript(string longName, ServiceController[] services, Form mainForm
			, DataGrid serviceGrid)
		{
            this.serviceScripts.Where( s => s.LongName.Equals( longName ) ).First().Run( services,mainForm,serviceGrid);
		}

		public void InitializeScripts(IScriptHelper withThisHelper)
		{
            foreach (var s in this.ftpFileScripts)
            {
                s.Initialize(withThisHelper);
            }
            foreach (var x in this.ftpFolderScripts)
            {
                x.Initialize(withThisHelper);
            }
            
			foreach ( var script in this.fileScripts )
			{
				script.Initialize( withThisHelper );
			}

			foreach ( var script in this.folderScripts )
			{
				script.Initialize( withThisHelper );
			}

            foreach (var script in this.ServiceScripts)
            {
                script.Initialize(withThisHelper);
            }
		}

		#endregion
	}
}
