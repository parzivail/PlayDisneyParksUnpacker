using System.Text;

namespace PlayDisneyParksUnpacker;

public class JsModule
{
	public string Name { get; }
	public string[] Imports { get; }
	public string[] ImportArgs { get; }

	public StringBuilder Content { get; }

	public JsModule(string name, string[] imports, string[] importArgs, StringBuilder content)
	{
		Name = name;
		Imports = imports;
		ImportArgs = importArgs;
		Content = content;
	}

	public JsModule(string name, string[] imports, string[] importArgs) : this(name, imports, importArgs, new StringBuilder())
	{
	}
}