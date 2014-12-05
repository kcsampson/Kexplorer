using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Data;
using System.ServiceProcess;
using System.Windows.Forms;
using Kexplorer.scripting;
using Kexplorer.res;
using System.Text.RegularExpressions;

namespace Kexplorer.services
{
    class AddMachineScript : BaseServiceScript
    {
        public AddMachineScript()
        {
            this.LongName = "Add Services";

            this.Active = true;

            this.Description = "Add a Machine'sLIst of Scripts";


        }

        DataView addView = null;

        string MachineName { get; set; }
        string SearchPattern { get; set; }

        public override void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid)
        {

            var addedMachine = QuickDialog2.DoQuickDialog("Add A Machine's Services", "Machine Name:",".","Pattern  ^(Enable|EPX):","");

            if (addedMachine != null)
            {

                MachineName = addedMachine[0];
                this.SearchPattern = addedMachine[1];
                try
                {
                    DataGrid dgrid = serviceGrid;

                    this.addView = (DataView)dgrid.DataSource;

                    CurrencyManager bm = (CurrencyManager)dgrid.BindingContext[this.addView];
                    ArrayList arrSelectedRows = new ArrayList();


                    this.addView = (DataView)bm.List;


                    mainForm.Invoke(new InvokeDelegate(this.AddMachineInGUIThread));



                    serviceGrid.Refresh();

                }
                finally
                {
                    addedMachine = null;
                    addView = null;
                }


            }
        }



        private void AddMachineInGUIThread()
        {
            
     
   
            var services =  System.ServiceProcess.ServiceController.GetServices(this.MachineName)
                            .Where( sc => string.IsNullOrEmpty(this.SearchPattern) ||
                               Regex.Match( sc.DisplayName, this.SearchPattern ).Length > 0 )
                            .ToList()
                            .OrderBy(s => s.ServiceName)
                            .ToList();


         //   MessageBox.Show( services.Aggregate(new StringBuilder(this.MachineName+" "), (curr,next) => curr.Append( "{" + next.DisplayName + ","+next.MachineName +"},")).ToString()
         //       , "Services Loaded");
            foreach (var service in services)
            {

                var row = this.addView.Table.NewRow();

                row["Name"] = service.DisplayName;

                row["Status"] = service.Status.ToString();


                row["SystemName"] = service.ServiceName;

                row["CanPauseAndContinue"] = service.CanPauseAndContinue;

                row["CanShutdown"] = service.CanShutdown;

                row["CanStop"] = service.CanStop;
                row["ServiceType"] = service.ServiceType.ToString();

                row["ServiceControllerObject"] = service;

                row["Machine"] = service.MachineName;

                this.addView.Table.Rows.Add(row);

            }





        }



    }
}
