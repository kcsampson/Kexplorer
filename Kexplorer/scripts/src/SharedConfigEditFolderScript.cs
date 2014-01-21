using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;


namespace Kexplorer.scripts
{
    class SharedConfigEditFolderScript : BaseFileAndFolderScript
    {


        public SharedConfigEditFolderScript(){
            this.LongName = "SharedConfig Tool";

            this.Description = "Open the Brandt Shared Config Tool.";

            this.Active = true;

           // this.ScriptShortCut = Shortcut.F2;

            this.ValidatorFolder = this.ValidateFolder;
        }



        /// <summary>
        /// Return true if we're on an epim folder.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool ValidateFolder(DirectoryInfo folder)
        {
            

            var result = false;
            string x = "";

            if (folder != null)
            {
                x = Directory.GetDirectoryRoot(folder.FullName);

            }


            if (folder != null
                && folder.FullName != Directory.GetDirectoryRoot( folder.FullName )
                && Directory.GetFiles(folder.FullName,"sharedConfig.properties",SearchOption.AllDirectories).Length>=0
                )
            {
                result = true;
            }


            return result;


        }

        public override void Run(KExplorerNode folder)
        {

            var fileInfo = new FileInfo(@".\jboss\server\default\conf\sharedConfig.properties");

            this.ScriptHelper.RunProgram("SharedConfigEditor.exe",  "\"" +fileInfo.FullName + "\"", folder, false);

        }

        public override void Run(KExplorerNode folder, FileInfo[] files )
        {

            var fileInfo = new FileInfo(@".\jboss\server\default\conf\sharedConfig.properties");

            this.ScriptHelper.RunProgram("SharedConfigEditor.exe", "\"" + fileInfo.FullName + "\"", folder, false);

        }

    }
}
