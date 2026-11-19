using System.Collections.Generic;
using Colossal;
using Game.Modding;

namespace Skyline_Sports_Project;

public class LocaleEN : IDictionarySource
{
	private readonly Setting m_Setting;

	public LocaleEN(Setting setting)
	{
		m_Setting = setting;
	}

	public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
	{
		return new Dictionary<string, string>
		{
			{
				((ModSetting)m_Setting).GetSettingsLocaleID(),
				"Skyline Sports"
			},
			{
				((ModSetting)m_Setting).GetOptionTabLocaleID("Main"),
				"Main"
			},
			{
				((ModSetting)m_Setting).GetOptionGroupLocaleID("Leagues"),
				"Leagues"
			},
			{
				((ModSetting)m_Setting).GetOptionGroupLocaleID("Display"),
				"Display"
			},
			{
				((ModSetting)m_Setting).GetOptionGroupLocaleID("Data"),
				"Data"
			},
			{
				((ModSetting)m_Setting).GetOptionGroupLocaleID("Fans"),
				"Fans"
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("RealFans"),
				"Real fans at Spotlight games"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("RealFans"),
				"Your residents go to each Spotlight game in your city. From about 2.5 hours before the start they leave home in waves, by car (parking nearby), by transit or on foot, and walk in; after the game they go home. The game page shows how many are inside. If it can't work in your game, it turns itself off and the game page says why."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("VisitorFans"),
				"Visitors from out of town"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("VisitorFans"),
				"With Real fans on: tourists staying in your city (in hotels, or out sightseeing) go to Spotlight games too, up to a quarter more fans, and go back to their hotel after the game."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("GameDayTourism"),
				"Game days draw tourists (experimental)"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("GameDayTourism"),
				"With Real fans on: on a Spotlight game's day, a venue that's an attraction in the game counts as a much bigger one, which is what brings tourists to a city, so more of them come in by plane, train, ship and car and stay in your hotels. A venue that isn't an attraction is left alone. If the game resets it by itself, it turns off and the log says so."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("RealFansCount"),
				"Fans per game"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("RealFansCount"),
				"How many residents go to each Spotlight game. Big crowds mean more traffic and fuller parking around the venue (and more work for your computer)."
			},
			{
				((ModSetting)m_Setting).GetOptionGroupLocaleID("About"),
				"About"
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("LeagueSize"),
				"Teams per league"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("LeagueSize"),
				"How many teams play in each league. Your city's teams come first, out of town rivals fill the rest. A season that hasn't started yet changes right away; a season under way keeps its teams and the change comes with the next season: new teams join, or the weakest out of town teams fold (their history stays, their players move on)."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("SeasonGames"),
				"Games per season"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("SeasonGames"),
				"100% plays the real number of games (82 in pro basketball, 162 in pro baseball, 17 in pro football). Lower it for lighter schedules: the season still runs on its real dates, with fewer games spread across it. Takes effect next season."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("InjuryFrequency"),
				"Injury frequency"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("InjuryFrequency"),
				"How often players get hurt. 100% is the mod's normal rate, already well below real life: mostly short injuries, long ones are rare, and a team never has more than a few players out at once. 0% turns injuries off."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("ShowLeagueButton"),
				"Show the Sports button"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("ShowLeagueButton"),
				"Show the button in the top left corner that opens schedules, scores and standings."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("Temperature"),
				"Temperature"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("Temperature"),
				"How game day temperatures show: the same as the game's own unit setting (metric shows Celsius), or always Celsius or Fahrenheit."
			},
			{
				((ModSetting)m_Setting).GetEnumValueLocaleID<Setting.TemperatureUnit>(Setting.TemperatureUnit.SameAsGame),
				"Same as the game"
			},
			{
				((ModSetting)m_Setting).GetEnumValueLocaleID<Setting.TemperatureUnit>(Setting.TemperatureUnit.Celsius),
				"Celsius (°C)"
			},
			{
				((ModSetting)m_Setting).GetEnumValueLocaleID<Setting.TemperatureUnit>(Setting.TemperatureUnit.Fahrenheit),
				"Fahrenheit (°F)"
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("ResetAllData"),
				"Reset all sports data"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("ResetAllData"),
				"Deletes every venue, team, schedule and result in the city that's loaded right now. Only works while a city is open."
			},
			{
				((ModSetting)m_Setting).GetOptionWarningLocaleID("ResetAllData"),
				"Delete all sports data for this city? This can't be undone (unless you reload an earlier save)."
			},
			{
				((ModSetting)m_Setting).GetOptionLabelLocaleID("Version"),
				"Version"
			},
			{
				((ModSetting)m_Setting).GetOptionDescLocaleID("Version"),
				"Skyline Sports version."
			}
		};
	}

	public void Unload()
	{
	}
}
