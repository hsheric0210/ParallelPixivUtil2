using System.IO;

namespace ParallelPixivUtil2
{
	public static class FileUtils
	{
		public static string PerformRollingFileRename(string fileName)
		{
			if (!File.Exists(fileName))
				return fileName;

			var i = 1;
			while (File.Exists($"{fileName}.{i}.bak"))
				i++;

			var newName = $"{fileName}.{i}.bak";
			File.Move(fileName, newName);
			return newName;
		}
	}
}
