using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripts
{
    class CopyShortNameToClipboard : BaseFileAndFolderScript
    {

        public CopyShortNameToClipboard()
        {
            this.LongName = "Name - Short Name to Clipboard";

            this.Description = "Copy the Short name of the selected folder or file to the clipboard.";

            this.Active = true;



        }
        public override void Run(KExplorerNode folder, FileInfo[] files)
        {
            if (files != null && files.Length > 0)
            {
                Clipboard.SetDataObject(files[0].Name);
            }
        }

        public override void Run(KExplorerNode folder)
        {
            Clipboard.SetDataObject(folder.DirInfo.Name, true);
        }
    }

}
