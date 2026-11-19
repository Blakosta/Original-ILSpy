using System;
using System.Collections.Generic;
using System.Text;

namespace Skyline_Sports_Project.Engine;

public class NameGenerator
{
	private readonly Random _random;

	public static readonly string[] Mascots = new string[80]
	{
		"Wildcats", "Eagles", "Hawks", "Falcons", "Tigers", "Lions", "Bears", "Wolves", "Panthers", "Bulldogs",
		"Mustangs", "Stallions", "Knights", "Warriors", "Titans", "Spartans", "Raiders", "Pirates", "Vikings", "Rangers",
		"Rockets", "Comets", "Blaze", "Thunder", "Storm", "Lightning", "Cyclones", "Hurricanes", "Rebels", "Chargers",
		"Cougars", "Jaguars", "Bobcats", "Owls", "Ravens", "Huskies", "Grizzlies", "Rams", "Bison", "Coyotes",
		"Foxes", "Sharks", "Mariners", "Pioneers", "Explorers", "Miners", "Lumberjacks", "Express", "Generals", "Admirals",
		"Monarchs", "Royals", "Dragons", "Griffins", "Hornets", "Yellowjackets", "Scorpions", "Vipers", "Rattlers", "Gators",
		"Stingrays", "Barracudas", "Flyers", "Jets", "Aces", "Outlaws", "Bandits", "Marshals", "Sentinels", "Guardians",
		"Patriots", "Cardinals", "Herons", "Pelicans", "Fury", "Force", "Legends", "Stars", "Meteors", "Tide"
	};

	public static readonly string[] HockeyMascots = new string[10] { "Blizzard", "Avalanche", "Glaciers", "Icebreakers", "Frost", "Polar Bears", "Freeze", "Wolverines", "Snowhawks", "Ice Dogs" };

	public static readonly string[] BaseballMascots = new string[10] { "Sluggers", "Mudcats", "Riverdogs", "Clippers", "Bats", "Diamondbacks", "Fireflies", "Sea Dogs", "Grasshoppers", "Barnstormers" };

	public static readonly string[] SoccerProNames = new string[10] { "FC", "United", "City", "SC", "Athletic", "Rovers", "Wanderers", "Republic", "Union", "Galaxy" };

	public static readonly string[] Places = new string[60]
	{
		"Port Haven", "Granite Falls", "Cedar Ridge", "Silverlake", "Bridgewater", "Northfield", "Ashford", "Kingsport", "Harbor City", "Iron Bay",
		"Clearwater Bay", "Maple Grove", "Stonebridge", "Fairhaven", "Westbrook", "Eastport", "Riverton", "Lakeshore", "Pine Bluff", "Copper Valley",
		"Summit City", "Bayview", "Highland Park", "Oakmont", "Brookfield", "Crestwood", "Glenhaven", "Wolf Creek", "Falcon Heights", "Elk River",
		"Sandy Point", "Red Rock", "Blue Mesa", "Golden Plains", "Fox Hollow", "Hartwell", "Millbrook", "Dunmore", "Aldridge", "Carver City",
		"Lexford", "Marlow", "Norwood", "Pembrook", "Quincy Falls", "Rockport", "Shelby Springs", "Tidewater", "Union City", "Vale City",
		"Windmere", "York Harbor", "Zephyr Bay", "Coral Coast", "Prairie View", "Twin Rivers", "Lone Pine", "Seaside", "Mountainview", "Kestrel Point"
	};

	public static readonly string[] HighSchoolNames = new string[40]
	{
		"Lincoln", "Washington", "Jefferson", "Roosevelt", "Kennedy", "Franklin", "Madison", "Central", "North", "South",
		"East", "West", "Riverside", "Oak Ridge", "Hillcrest", "Lakeview", "Valley", "Westfield", "Ridgeview", "Summit",
		"Parkside", "Northgate", "Southwood", "Eastlake", "Westwood", "Pinecrest", "Mountain View", "Harbor", "Bayside", "Meadowbrook",
		"Fairview", "Brookside", "Crestview", "Glenwood", "Cedar Grove", "Maple Heights", "Liberty", "Heritage", "Pioneer", "Sunset"
	};

	private static readonly string[] HighSchoolSuffixes = new string[5] { "North", "South", "East", "West", "Central" };

	public NameGenerator(Random random)
	{
		_random = random;
	}

	public string PickMascot(Sport sport, Level level, ICollection<string> taken, string place = null)
	{
		List<string> pool = new List<string>();
		if (level == Level.Pro && sport == Sport.Soccer)
		{
			pool.AddRange(SoccerProNames);
			if (!string.IsNullOrEmpty(place))
			{
				pool.RemoveAll((string x) => place.EndsWith(" " + x, StringComparison.OrdinalIgnoreCase) || place.Equals(x, StringComparison.OrdinalIgnoreCase));
			}
		}
		else
		{
			pool.AddRange(Mascots);
			if (sport == Sport.Hockey)
			{
				pool.AddRange(HockeyMascots);
			}
			if (sport == Sport.Baseball)
			{
				pool.AddRange(BaseballMascots);
			}
		}
		return PickUnique(pool, taken);
	}

	public string PickSchoolMascot(ICollection<string> taken)
	{
		return PickUnique(new List<string>(Mascots), taken);
	}

	public string PickPlace(ICollection<string> taken)
	{
		return PickUnique(new List<string>(Places), taken);
	}

	public string HighSchoolName(string districtName, ICollection<string> takenNames)
	{
		string baseName = CleanPlace(districtName);
		string[] highSchoolSuffixes;
		if (baseName.Length > 0)
		{
			string candidate = baseName + " High";
			if (!Contains(takenNames, candidate))
			{
				return candidate;
			}
			highSchoolSuffixes = HighSchoolSuffixes;
			foreach (string suffix in highSchoolSuffixes)
			{
				candidate = baseName + " " + suffix + " High";
				if (!Contains(takenNames, candidate))
				{
					return candidate;
				}
			}
		}
		List<string> options = new List<string>();
		highSchoolSuffixes = HighSchoolNames;
		for (int i = 0; i < highSchoolSuffixes.Length; i++)
		{
			string candidate2 = highSchoolSuffixes[i] + " High";
			if (!Contains(takenNames, candidate2))
			{
				options.Add(candidate2);
			}
		}
		if (options.Count > 0)
		{
			return options[_random.Next(options.Count)];
		}
		return HighSchoolNames[_random.Next(HighSchoolNames.Length)] + " High " + _random.Next(2, 99);
	}

	public string CollegeName(string place, ICollection<string> takenNames)
	{
		string p = CleanPlace(place);
		if (p.Length == 0)
		{
			p = Places[_random.Next(Places.Length)];
		}
		List<string> styles = new List<string>
		{
			"University of " + p,
			p + " State",
			p + " College",
			p + " Tech"
		};
		Shuffle(styles);
		foreach (string s in styles)
		{
			if (!Contains(takenNames, s))
			{
				return s;
			}
		}
		return styles[0] + " " + HighSchoolSuffixes[_random.Next(HighSchoolSuffixes.Length)];
	}

	public static string Abbreviation(string name)
	{
		string[] skip = new string[9] { "high", "university", "of", "state", "college", "tech", "the", "fc", "sc" };
		List<string> words = new List<string>();
		string[] array = (name ?? "").Split(' ', StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string w = array[i].Trim();
			if (w.Length == 0)
			{
				continue;
			}
			bool skipped = false;
			string[] array2 = skip;
			foreach (string s in array2)
			{
				if (string.Equals(w, s, StringComparison.OrdinalIgnoreCase))
				{
					skipped = true;
					break;
				}
			}
			if (!skipped)
			{
				words.Add(w);
			}
		}
		if (words.Count == 0)
		{
			words.Add(string.IsNullOrEmpty(name) ? "TBD" : name.Trim());
		}
		StringBuilder sb = new StringBuilder();
		if (words.Count == 1)
		{
			string text = words[0];
			foreach (char c in text)
			{
				if (char.IsLetterOrDigit(c))
				{
					sb.Append(c);
				}
				if (sb.Length == 3)
				{
					break;
				}
			}
		}
		else
		{
			for (int k = 0; k < words.Count; k++)
			{
				if (sb.Length >= 3)
				{
					break;
				}
				char c2 = FirstLetter(words[k]);
				if (c2 != '\0')
				{
					sb.Append(c2);
				}
			}
			string last = words[words.Count - 1];
			for (int l = 1; l < last.Length; l++)
			{
				if (sb.Length >= 3)
				{
					break;
				}
				if (char.IsLetterOrDigit(last[l]))
				{
					sb.Append(last[l]);
				}
			}
		}
		if (sb.Length == 0)
		{
			sb.Append("TBD");
		}
		return sb.ToString().ToUpperInvariant();
	}

	public static string CleanName(string value, int maxLength)
	{
		if (value == null)
		{
			return "";
		}
		StringBuilder sb = new StringBuilder();
		foreach (char c in value)
		{
			if (!char.IsControl(c))
			{
				sb.Append(c);
			}
		}
		string result = sb.ToString().Trim();
		while (result.Contains("  "))
		{
			result = result.Replace("  ", " ");
		}
		if (result.Length > maxLength)
		{
			result = result.Substring(0, maxLength).Trim();
		}
		return result;
	}

	private static string CleanPlace(string place)
	{
		string p = CleanName(place, 32);
		if (p.StartsWith("District", StringComparison.OrdinalIgnoreCase))
		{
			return "";
		}
		return p;
	}

	private static char FirstLetter(string word)
	{
		foreach (char c in word)
		{
			if (char.IsLetterOrDigit(c))
			{
				return c;
			}
		}
		return '\0';
	}

	private string PickUnique(List<string> pool, ICollection<string> taken)
	{
		List<string> free = new List<string>();
		foreach (string s in pool)
		{
			if (!Contains(taken, s))
			{
				free.Add(s);
			}
		}
		if (free.Count > 0)
		{
			return free[_random.Next(free.Count)];
		}
		return pool[_random.Next(pool.Count)];
	}

	private static bool Contains(ICollection<string> set, string value)
	{
		if (set == null)
		{
			return false;
		}
		foreach (string item in set)
		{
			if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = _random.Next(i + 1);
			T tmp = list[i];
			list[i] = list[j];
			list[j] = tmp;
		}
	}
}
