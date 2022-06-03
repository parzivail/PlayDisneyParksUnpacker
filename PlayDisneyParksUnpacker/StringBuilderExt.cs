using System.Text;

namespace PlayDisneyParksUnpacker;

public static class StringBuilderExt
{
	/// <summary>
	/// Returns the index of the start of the contents in a StringBuilder
	/// </summary>
	/// <param name="sb">The target StringBuilder</param>
	/// <param name="value">The string to find</param>
	/// <param name="startIndex">The starting index.</param>
	/// <remarks>https://stackoverflow.com/questions/1359948/why-doesnt-stringbuilder-have-indexof-method</remarks>
	/// <returns></returns>
	public static int IndexOf(this StringBuilder sb, string value, int startIndex = 0)
	{
		var length = value.Length;
		var maxSearchLength = sb.Length - length + 1;

		for (var i = startIndex; i < maxSearchLength; ++i)
		{
			if (sb[i] != value[0]) continue;

			var index = 1;
			while (index < length && sb[i + index] == value[index])
				++index;

			if (index == length)
				return i;
		}

		return -1;
	}
}