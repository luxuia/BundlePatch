using System;
namespace BundlePatch
{
	public class PatchUtil
	{
		public class PatchInfoData
		{
			public class Patch
			{
				public int offset;
				public int size;
				public bool is_patch;
			}

			public List<Patch> infos = new List<Patch>();

			public string srcFileName;
			public string dstFileName;
			public string patchFileName;

			public string srcMD5;
			public string dstMD5;
		}

		public static string DummyHeader = "XX19";

		public PatchUtil()
		{
		}
	}
}

