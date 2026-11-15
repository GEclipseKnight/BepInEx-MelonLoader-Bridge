namespace MelonLoader.ICSharpCode.SharpZipLib.Core
{
	public interface INameTransform
	{
		string TransformFile(string name);

		string TransformDirectory(string name);
	}
}
