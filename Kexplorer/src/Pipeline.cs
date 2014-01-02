using System;
using System.Collections;
using System.Threading;

namespace Kexplorer
{
	/// <summary>
	/// The pipeline of work to be done, also defines the work items.
	/// The Pipeline runs a background thread that monitors the pipeline (queue) for
	/// work to be done.  Does the work one thing at a time.
	/// </summary>
	public class Pipeline
	{

		#region Member variables -------------------------------------------------------

		private Queue queue = new Queue();

		private Thread worker = null;
		private bool stop = false;



		private ISimpleKexplorerGUI form = null;

		private IWorkUnit highPriorityJob = null;
		private bool stopHighPriorityJob = false;

		private Thread highPriorityThread = null;

		private IWorkUnit moreWorkToDoFromThread = null;
		private IWorkUnit currentNormalJob = null;
		private Thread lowPriorityThread = null;
		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		/// <summary>
		///  Simple Constructor
		/// </summary>
		public Pipeline( ISimpleKexplorerGUI newForm)
		{

			this.form = newForm;

		}
		#endregion ----------------------------------------------------------------------

		#region Public Methods ----------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Start the background thread.
		/// </summary>
		public void StartWork()
		{
			this.worker = new Thread( new ThreadStart( this.GetToWork ));

			this.worker.Start();
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Set a flag to pause the job loop.
		/// Once the job loop sets flag it is paused, then start this highpriority job.
		/// </summary>
		/// <param name="job"></param>
		public void AddPriorityJob( IWorkUnit job )
		{

				
				this.stopHighPriorityJob = true;
			
				while ( this.highPriorityJob != null )
				{
					Thread.Sleep(25);

				}
				this.stopHighPriorityJob = false;
				this.highPriorityJob = job;

		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Stops the running thread.
		/// </summary>
		public void StopWork()
		{
			this.stop = true;
			lock( this.queue )
			{
				this.queue.Clear();
			}
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Adds some work to the queue.
		/// </summary>
		/// <param name="job"></param>
		public void  AddJob(IWorkUnit job)
		{
			lock( this.queue )
			{
				if ( !this.stop )
				{
					this.queue.Enqueue( job );
				}
			}

		}
		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// The actual running thread.
		/// Monitor the queue for work to be done.  On asking jobs to do their work,
		/// they may return a new task to add to the queue.
		/// </summary>
		private void GetToWork()
		{
			while ( !this.form.MainForm.Visible )
			{
				Thread.Sleep( 100 );
			}

			while ( !stop )
			{
				for ( int i = 0; i < 100; i++ )
				{

						// Check for pausing..
					while( this.stopHighPriorityJob && this.highPriorityJob != null )
					{


						this.highPriorityJob.Abort();
						if ( this.highPriorityThread != null )
						{
							this.highPriorityThread.Abort();
						}
		
						Thread.Sleep(10);

					}
					if (this.highPriorityJob != null && this.highPriorityThread == null )
					{
						this.highPriorityThread = new Thread( new ThreadStart(this.RunHighPriorityJob ));
						this.highPriorityThread.Start();
					}
					
					if ( this.highPriorityJob != null )
					{

						Thread.Sleep(10 );
						continue;
					}
						
					
				

					if ( this.queue.Count > 0 )
					{
						IWorkUnit x = null;
						lock( this.queue )
						{
							x = (IWorkUnit)this.queue.Dequeue();
						}

						this.moreWorkToDoFromThread = null;
						this.currentNormalJob = x;
						this.lowPriorityThread = new Thread( new ThreadStart( this.RunLowPriorityJob ));
						DateTime startTime = DateTime.Now;
						this.lowPriorityThread.Start();


						while ( this.lowPriorityThread != null )
						{
							int j = 10;
							while ( --j > 0 && this.lowPriorityThread != null )
							{
								Thread.Sleep(10);
							}
							// Check on the lowpriority thread every 40 miliseconds.
							if ( this.lowPriorityThread != null )
							{
								TimeSpan timeSpan = DateTime.Now.Subtract( startTime );
								if ( timeSpan.Milliseconds > 5000 )
								{
									// It's taking too long  abort it.
									this.lowPriorityThread.Abort();
									if ( this.currentNormalJob != null ){
										this.currentNormalJob.YouWereAborted();
									}

								}
							}

						}

						IWorkUnit moreWorkToDo = this.moreWorkToDoFromThread;

						if ( moreWorkToDo != null )
						{
							this.AddJob( moreWorkToDo );
						}
						
					}
				}

				Thread.Sleep(10);
			}
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Thread that runs a single low priority job.
		/// </summary>
		private void RunLowPriorityJob()
		{
			try 
			{
				this.moreWorkToDoFromThread = this.currentNormalJob.DoJob();
			} 
			catch ( System.Threading.ThreadAbortException )
			{
				DirPerfStat.Instance().IncrementLowPriorityThreadAborts();
			}
			finally 
			{
				this.lowPriorityThread = null;
			}
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Run the high priority job in a separate thread.
		/// </summary>
		private void RunHighPriorityJob()
		{
			try 
			{
				if ( this.highPriorityJob != null )
				{
					IWorkUnit moreWork = null;
					try 
					{
						moreWork = this.highPriorityJob.DoJob();
					} 
					finally 
					{
						this.highPriorityJob = null;
					}
					if ( moreWork != null )
					{
						this.AddJob( moreWork );
					}
				}
			} 
			catch ( System.Threading.ThreadAbortException )
			{
			
				DirPerfStat.Instance().IncrementHighPriorityThreadAborts();
			}

			finally 
			{
				this.highPriorityJob = null;
				this.highPriorityThread = null;
			}
		}
		#endregion ----------------------------------------------------------------------


	}
}
