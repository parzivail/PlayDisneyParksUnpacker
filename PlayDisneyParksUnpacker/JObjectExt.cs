using Newtonsoft.Json.Linq;

namespace PlayDisneyParksUnpacker;

public static class JObjectExt
{
	/// <summary>
	/// Sorts a JObject by its keys
	/// </summary>
	/// <remarks>https://stackoverflow.com/questions/14417235/c-sharp-sort-json-string-keys</remarks>
	/// <param name="jObj"></param>
	public static void Sort(this JObject jObj)
	{
		var props = jObj.Properties().ToList();
		foreach (var prop in props)
			prop.Remove();

		foreach (var prop in props.OrderBy(p => p.Name))
		{
			jObj.Add(prop);
			switch (prop.Value)
			{
				case JObject value:
					value.Sort();
					break;
				case JArray:
				{
					var numArrayValues = prop.Value.Count();
					for (var i = 0; i < numArrayValues; i++)
						if (prop.Value[i] is JObject o)
							o.Sort();
					break;
				}
			}
		}
	}
}