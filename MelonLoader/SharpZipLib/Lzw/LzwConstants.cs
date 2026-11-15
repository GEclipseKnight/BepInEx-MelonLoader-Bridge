namespace MelonLoader.ICSharpCode.SharpZipLib.Lzw
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "kept for backwards compatibility")]
	sealed public class LzwConstants
	{
		public const int MAGIC = 0x1f9d;

		public const int MAX_BITS = 16;

		/* 3rd header byte:
         * bit 0..4 Number of compression bits
         * bit 5    Extended header
         * bit 6    Free
         * bit 7    Block mode
         */

		public const int BIT_MASK = 0x1f;

		public const int EXTENDED_MASK = 0x20;


		public const int RESERVED_MASK = 0x60;

		public const int BLOCK_MODE_MASK = 0x80;

		public const int HDR_SIZE = 3;

		public const int INIT_BITS = 9;

		private LzwConstants()
		{
		}
	}
}
