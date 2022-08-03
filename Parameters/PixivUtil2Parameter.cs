namespace ParallelPixivUtil2.Parameters
{
	public sealed record PixivUtil2Parameter(string Executable, string PythonExecutable, string PythonScript, bool IsPythonScript, string WorkingDirectory, string LogPath) : AbstractParameter
	{
		public override string FileName => IsPythonScript ? PythonExecutable : Executable;

		public override string ExtraParameters => IsPythonScript ? (PythonScript + ' ') : "";

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				var dict = new Dictionary<string, string>
				{
					["logPath"] = LogPath,
					["ipcAddress"] = Identifier + '|' + Ipc.IPCCommunicationAddress + '|' + Ipc.IPCTaskAddress
				};

				if (DatabasePath != null)
					dict["databasePath"] = DatabasePath;

				if (Aria2InputPath != null)
					dict["aria2InputPath"] = Aria2InputPath;

				if (Member != null)
				{
					dict["memberID"] = Member?.MemberID.ToString()!;
					dict["page"] = Member?.Page!.Page.ToString()!;
					dict["fileIndex"] = Member?.Page!.FileIndex.ToString()!;
				}

				if (MemberDataListFile != null)
					dict["memberDataList"] = MemberDataListFile;

				return dict;
			}
		}

		public string? Aria2InputPath
		{
			get; set;
		}

		public string? DatabasePath
		{
			get; set;
		}

		public string? MemberDataListFile
		{
			get; set;
		}

		public string? Identifier
		{
			get; set;
		}

		public MemberSubParameter? Member
		{
			get; set;
		}

		public IpcSubParameter Ipc
		{
			get; set;
		}
	}
}
