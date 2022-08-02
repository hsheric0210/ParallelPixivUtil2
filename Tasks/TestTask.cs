namespace ParallelPixivUtil2.Tasks
{
	public class TestTask : AbstractTask
	{
		public TestTask() : base("Test Task")
		{
		}

		protected override bool Run()
		{
			Indeterminate = false;

			while (true)
			{
				CurrentProgress = Random.Shared.Next(TotalProgress);
				TaskName = Random.Shared.Next(TotalProgress).ToString();
				Error = Random.Shared.NextSingle() > 0.6f;
				Thread.Sleep(100);
			}
		}
	}
}
