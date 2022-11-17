using log4net;
using ParallelPixivUtil2.Parameters;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class ParseMemberDataListTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(ParseMemberDataListTask));

		private readonly string MemberDataList;

		public IDictionary<long, ICollection<MemberPage>> Parsed
		{
			get; private set;
		} = null!;

		public int TotalImageCount
		{
			get; private set;
		}

		public int TotalPageCount
		{
			get; private set;
		}

		public ParseMemberDataListTask(string memberDataList) : base("Parsing member data list file") => MemberDataList = memberDataList;

		protected override bool Run()
		{
			int maxImagesPerPage = App.Configuration.Magics.MaxImagesPerPage;
			var memberPageList = new Dictionary<long, ICollection<MemberPage>>();
			foreach (string line in File.ReadAllLines(MemberDataList))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				string[] pieces = line.Split(',');
				if (!long.TryParse(pieces[0], out long memberId) || !int.TryParse(pieces[1], out int memberTotalImages))
					continue;

				if (memberTotalImages > 0)
				{
					if (!memberPageList.ContainsKey(memberId))
						memberPageList[memberId] = new List<MemberPage>();

					int pageCount = (memberTotalImages - memberTotalImages % maxImagesPerPage) / maxImagesPerPage + 1;
					for (int i = 1; i <= pageCount; i++)
						memberPageList[memberId].Add(new MemberPage(i, pageCount - i + 1));
					TotalPageCount += pageCount;
					TotalImageCount += memberTotalImages;
					Logger.DebugFormat("Member {0} has {1} images -> {2} pages.", memberId, memberTotalImages, pageCount);
				}
				else
				{
					Logger.WarnFormat("Member {0} doesn't have any images! Skipping.", memberId);
				}
			}

			Parsed = memberPageList;
			return false;
		}
	}
}
