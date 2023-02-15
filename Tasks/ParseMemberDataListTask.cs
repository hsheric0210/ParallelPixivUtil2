using ParallelPixivUtil2.Parameters;
using Serilog;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class ParseMemberDataListTask : AbstractTask
	{
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
			var maxImagesPerPage = App.Configuration.Magics.MaxImagesPerPage;
			var memberPageList = new Dictionary<long, ICollection<MemberPage>>();
			foreach (var line in File.ReadAllLines(MemberDataList))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var pieces = line.Split(',');
				if (!long.TryParse(pieces[0], out var memberId) || !int.TryParse(pieces[1], out var memberTotalImages))
					continue;

				if (memberTotalImages > 0)
				{
					if (!memberPageList.ContainsKey(memberId))
						memberPageList[memberId] = new List<MemberPage>();

					var pageCount = (memberTotalImages - memberTotalImages % maxImagesPerPage) / maxImagesPerPage + 1;
					for (var i = 1; i <= pageCount; i++)
						memberPageList[memberId].Add(new MemberPage(i, pageCount - i + 1));
					TotalPageCount += pageCount;
					TotalImageCount += memberTotalImages;
					Log.Debug("Member {0} has {1} images -> {2} pages.", memberId, memberTotalImages, pageCount);
				}
				else
				{
					Log.Warning("Member {0} doesn't have any images! Skipping.", memberId);
				}
			}

			Parsed = memberPageList;
			return false;
		}
	}
}
