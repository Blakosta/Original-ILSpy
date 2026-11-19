using System.Collections.Generic;

namespace Skyline_Sports_Project.Engine;

public class ReleaseQuote
{
	public List<int> Amounts = new List<int>();

	public int Total;

	public int Saved;

	public string Label = "Release";

	public string Verb = "released";

	public string Reason = "Released";

	public string Short = "";

	public string Rule = "";

	public bool UsesBuyout;

	public bool CanStretch;
}
