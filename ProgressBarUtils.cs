using ShellProgressBar;

namespace ParallelPixivUtil2
{
	public static class ProgressBarUtils
	{
		private static readonly ProgressBarOptions DefaultProgressBarOpts = new()
		{
			BackgroundCharacter = '-',
			ProgressCharacter = '=',
			ForegroundColor = ConsoleColor.Blue,
			ForegroundColorError = ConsoleColor.Red,
			BackgroundColor = ConsoleColor.DarkGray,
			CollapseWhenFinished = true
		};

		private static ProgressBar? Global;

		public static void SetGlobal(int maxTicks, string name)
		{
			Global?.Dispose();
			Global = new ProgressBar(maxTicks, name, DefaultProgressBarOpts);
			ProgressBarCompatibleConsoleAppender.ProgressBar = Global;
		}

		public static ChildProgressBar? SpawnChild(int maxTicks, string name) => Global?.Spawn(maxTicks, name, DefaultProgressBarOpts);

		public static IndeterminateChildProgressBar? SpawnIndeterminateChild(string name) => Global?.SpawnIndeterminate(name, DefaultProgressBarOpts);

		public static void Done(this ChildProgressBar? childProgressBar)
		{
			childProgressBar?.Tick(childProgressBar.MaxTicks);
			Global?.Tick();
		}

		public static void Done(this IndeterminateChildProgressBar? childProgressBar)
		{
			childProgressBar?.Finished();
			Global?.Tick();
		}

		public static void TickGlobal(string? message = null) => Global?.Tick(message);

		public static void TickGlobal(int tick, string? message = null) => Global?.Tick(tick, message);
	}
}
